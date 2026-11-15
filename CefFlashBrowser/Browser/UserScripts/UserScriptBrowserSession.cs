using CefFlashBrowser.Data;
using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Log;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using CefFlashBrowser.ViewModels;
using CefSharp;
using Mzying2001.MonkeySharp.CefSharp;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Repository;
using Mzying2001.MonkeySharp.Core.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptBrowserSession
    {
        private readonly ChromiumFlashBrowser _browser;
        private readonly BrowserViewModel _model;
        private readonly UserScriptService _service;
        private readonly ILogger _logger;
        private readonly Action _mount;
        private readonly Dispatcher _dispatcher;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly SemaphoreSlim _navigation = new SemaphoreSlim(1, 1);
        private readonly IDisposable _registration;
        private readonly object _logLock = new object();
        private readonly Dictionary<ScriptKey, Tuple<long, int>> _logCounts = new Dictionary<ScriptKey, Tuple<long, int>>();
        private Task _initialization, _stop;
        private CefSharpUserScriptHost _host;
        private bool _repositorySubscribed;
        private bool _initialBlankTarget;
        private readonly UserScriptTabService _tabs;
        private readonly Func<Window> _ownerWindow;
        private UserScriptNotificationService _notifications;
        private UserScriptDownloadService _downloads;
        private readonly CefSharpTabAudioService _audio;
        private CefSharpHttpRequestService _http;
        private InMemoryWebRequestService _webRequests;
        private IRequestContext _requestContext;
        private bool _ownsRequestContext;
        private readonly List<RetiredHost> _retiredHosts = new List<RetiredHost>();
        private UserScriptMenuService _menus;
        private UserScriptContextMenuHandler _contextMenu;
        private SqliteUserScriptTabStateService _tabState;

        public CefSharpUserScriptHost Host => _host;
        public bool IsStopped => _stop?.IsCompleted == true;

        public UserScriptBrowserSession(ChromiumFlashBrowser browser, BrowserViewModel model,
            UserScriptService service, ILogger logger, Action mount, Func<Window> owner, Func<Task<bool>> close)
        {
            _browser = browser;
            _model = model;
            _service = service;
            _logger = logger;
            _mount = mount;
            _dispatcher = browser.Dispatcher;
            _ownerWindow = owner;
            _tabs = new UserScriptTabService(model, owner, close, _dispatcher, _lifetime.Token);
            _audio = new CefSharpTabAudioService(() => _browser.IsDisposed || !_browser.IsBrowserInitialized ? null : _browser.GetBrowser()?.GetHost());
            _audio.StateChanged += AudioStateChanged;
            _registration = service.RegisterSession(model.BrowserId, StopAsync);
            _browser.AddressChanged += AddressChanged;
            _browser.NavigationHandler = NavigateAsync;
        }

        public Task StartAsync()
        {
            _dispatcher.VerifyAccess();
            return _initialization ?? (_initialization = InitializeCoreAsync());
        }

        private async Task InitializeCoreAsync()
        {
            await Task.Yield();
            try
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                SetState(UserScriptState.Initializing);
                if (GlobalData.Settings.EnableUserScripts)
                {
                    await WaitAsync(_service.InitializeAsync(), _lifetime.Token);
                    _lifetime.Token.ThrowIfCancellationRequested();
                    _service.Repository.Changed += RepositoryChanged;
                    _repositorySubscribed = true;
                    if (AllowsScripts(_model.Address ?? "about:blank"))
                        BuildHost();
                }
                var needsBlankDocument = !_browser.IsNativeControlCreated;
                _mount();
                if (_host != null)
                {
                    await WaitForNativeAsync(_lifetime.Token);
                    if (needsBlankDocument)
                    {
                        _initialBlankTarget = _model.Address == "about:blank";
                        _browser.LoadPrepared("about:blank");
                    }
                    await _host.PrepareAsync(_lifetime.Token);
                    SetState(UserScriptState.Ready);
                }
                else
                {
                    await WaitForNativeAsync(_lifetime.Token);
                    SetState(UserScriptState.Disabled);
                }
                var audio = await _audio.GetStateAsync(_lifetime.Token);
                if (!_lifetime.IsCancellationRequested) _model.UpdateMuteState(audio.IsMuted);
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                await DetachHostAsync();
                if (!_lifetime.IsCancellationRequested)
                {
                    SetState(UserScriptState.Unavailable, e.Message);
                    Log(LogLevel.Error, "Userscript initialization failed (" + e.GetType().Name + ") for tab " + _model.BrowserId);
                    _mount();
                    try { await WaitForNativeAsync(_lifetime.Token); }
                    catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
                    catch (Exception) { Log(LogLevel.Warning, "Native fallback initialization did not finish for tab " + _model.BrowserId); }
                }
            }
        }

        private void BuildHost()
        {
            var render = _browser.RenderProcessMessageHandler;
            if (!(render is RenderProcessMessageHandlerMultiplexer))
                _browser.RenderProcessMessageHandler = render == null
                    ? new RenderProcessMessageHandlerMultiplexer() : new RenderProcessMessageHandlerMultiplexer(render);
            var request = _browser.RequestHandler;
            if (!(request is CefSharpWebRequestHandlerMultiplexer))
            {
                var multiplexer = new CefSharpWebRequestHandlerMultiplexer();
                if (request != null) multiplexer.Add(request);
                _browser.RequestHandler = multiplexer;
            }

            // A browser session owns its network context across host replacements.
            // Accepted downloads may outlive their originating document/host.
            if (_http == null)
            {
                _requestContext = _browser.RequestContext;
                _ownsRequestContext = _requestContext == null;
                if (_requestContext == null) _requestContext = Cef.GetGlobalRequestContext();
                _webRequests = new InMemoryWebRequestService();
                _http = new CefSharpHttpRequestService(_requestContext, _webRequests);
            }
            var applicationMenu = _browser.MenuHandler as Handlers.ContextMenuHandler;
            _menus = new UserScriptMenuService(_model, _dispatcher, _service.Repository, () => _host,
                () => Log(LogLevel.Warning, "Userscript menu callback failed for tab " + _model.BrowserId));
            if (_tabState == null) _tabState = _service.CreateTabStateService(_model.BrowserId);
            _notifications = new UserScriptNotificationService(_dispatcher, _ownerWindow, _tabs, _service.Content, _lifetime.Token,
                () => Log(LogLevel.Warning, "Userscript notification content or interaction was unavailable for tab " + _model.BrowserId));
            var builder = new CefSharpUserScriptHostBuilder(_service.Repository)
                .UseValueStore(_service.ValueStore)
                .UseHttpRequestService(_http)
                .UseWebRequestService(_webRequests)
                .UseTabAudioService(_audio)
                .UseDownloadServiceFactory(http => _downloads = new UserScriptDownloadService(http, _model, _ownerWindow,
                    _dispatcher, string.IsNullOrWhiteSpace(GlobalData.Settings.UserScriptDownloadDirectory)
                        ? UserScriptDownloadPaths.DefaultDirectory() : GlobalData.Settings.UserScriptDownloadDirectory, _lifetime.Token))
                .UseMenuService(_menus)
                .UseClipboardService(new UserScriptClipboardService(_dispatcher, _lifetime.Token))
                .UseNotificationService(_notifications)
                .UseTabService(_tabs)
                .UseWindowService(_tabs)
                .UseTabStateService(_tabState)
                .UsePermissionPolicy(new UserScriptExecutionPolicy(_service.PermissionPolicy,
                    () => GlobalData.Settings.TrustedUserScriptPageWorld, url => AllowsScripts(url.AbsoluteUri)))
                .Configure(new CefSharpHostOptions
                {
                    TrustedPageWorld = GlobalData.Settings.TrustedUserScriptPageWorld,
                    // This application only creates the normal global CEF context.
                    IsIncognito = context => false,
                    AutomaticallyUpdatesScripts = false
                })
                .LogTo(LogScript);
            _host = builder.Build();
            _host.Diagnostic += Diagnostic;
            _host.Attach(_browser);
            _contextMenu = new UserScriptContextMenuHandler(_browser.MenuHandler, _menus,
                () => applicationMenu?.LastMenuItemCount ?? 0,
                () => Log(LogLevel.Warning, "Userscript native menu limit reached for tab " + _model.BrowserId));
            _browser.MenuHandler = _contextMenu;
        }

        public async Task NavigateAsync(string address, CancellationToken token)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token))
            {
                await WaitAsync(StartAsync(), linked.Token);
                await _navigation.WaitAsync(linked.Token);
                var navigationStarted = false;
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    if (!GlobalData.Settings.EnableUserScripts || !AllowsScripts(address))
                    {
                        await DetachHostAsync();
                        linked.Token.ThrowIfCancellationRequested();
                        SetState(UserScriptState.Disabled);
                        _browser.LoadPrepared(address);
                    }
                    else if (_model.UserScriptState == UserScriptState.Unavailable)
                    {
                        _browser.LoadPrepared(address);
                    }
                    else
                    {
                        if (_host == null)
                        {
                            await WaitAsync(_service.InitializeAsync(), linked.Token);
                            BuildHost();
                        }
                        await _host.PrepareAsync(linked.Token);
                        linked.Token.ThrowIfCancellationRequested();
                        SetState(UserScriptState.Ready);
                        navigationStarted = true;
                        var alreadyAtInitialTarget = _initialBlankTarget && address == "about:blank";
                        _initialBlankTarget = false;
                        if (!alreadyAtInitialTarget)
                        {
                            if (Uri.TryCreate(address, UriKind.Absolute, out var url)
                                && (url.Scheme == "http" || url.Scheme == "https" || address == "about:blank"))
                                await _host.NavigateAsync(address, linked.Token);
                            else
                                _browser.LoadPrepared(address);
                        }
                        SetState(UserScriptState.Ready);
                    }
                    await UpdateMatchCountAsync(address);
                }
                catch (OperationCanceledException) when (linked.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception) when (navigationStarted)
                {
                    // Page.navigate reports normal load failures, including user
                    // Stop as ERR_ABORTED. A second Load would restart that request.
                    Log(LogLevel.Info, "Browser navigation ended before completion for tab " + _model.BrowserId);
                }
                catch (Exception e)
                {
                    await DetachHostAsync();
                    SetState(UserScriptState.Unavailable, e.Message);
                    Log(LogLevel.Error, "Userscript preparation failed for tab " + _model.BrowserId);
                    if (!linked.IsCancellationRequested)
                        _browser.LoadPrepared(address);
                }
                finally { _navigation.Release(); }
            }
        }

        public async Task RetryAsync()
        {
            _dispatcher.VerifyAccess();
            if (_stop != null)
                return;
            var address = _model.Address;
            await _navigation.WaitAsync(_lifetime.Token);
            try
            {
                await DetachHostAsync();
                if (_repositorySubscribed)
                {
                    _service.Repository.Changed -= RepositoryChanged;
                    _repositorySubscribed = false;
                }
                _initialization = null;
                await StartAsync();
            }
            finally { _navigation.Release(); }
            await NavigateAsync(address, _lifetime.Token);
        }

        public Task StopAsync()
        {
            if (!_dispatcher.CheckAccess())
                return _dispatcher.InvokeAsync(StopAsync).Task.Unwrap();
            return _stop ?? (_stop = StopCoreAsync());
        }

        private async Task StopCoreAsync()
        {
            await Task.Yield();
            SetState(UserScriptState.Stopping);
            _lifetime.Cancel();
            foreach (var retired in _retiredHosts.ToArray()) retired.Downloads.Dispose();
            if (_initialization != null)
            {
                try { await _initialization; }
                catch (Exception) { Log(LogLevel.Warning, "Userscript initialization ended during close for tab " + _model.BrowserId); }
            }
            await _navigation.WaitAsync();
            try
            {
                _browser.AddressChanged -= AddressChanged;
                if (_repositorySubscribed)
                    _service.Repository.Changed -= RepositoryChanged;
                if (!_browser.IsDisposed && _browser.IsBrowserInitialized)
                    CefSharp.WebBrowserExtensions.Stop(_browser);
                await DetachHostAsync(true);
            }
            finally
            {
                try
                {
                    await Task.WhenAll(_retiredHosts.Select(retired => retired.Drain).ToArray());
                    _audio.StateChanged -= AudioStateChanged;
                    _audio.Dispose();
                    _http?.Dispose();
                    if (_http != null) await Cef.UIThreadTaskFactory.StartNew(() => { });
                }
                finally
                {
                    _webRequests?.Dispose();
                    if (_ownsRequestContext) _requestContext?.Dispose();
                    try { if (_tabState != null) await _tabState.CloseAsync(); }
                    finally
                    {
                        _registration.Dispose();
                        SetState(UserScriptState.Stopped);
                        _navigation.Release();
                    }
                }
            }
        }

        private async Task DetachHostAsync(bool closing = false)
        {
            var host = _host;
            if (host == null) return;
            var downloads = _downloads;
            var notifications = _notifications;
            var menus = _menus;
            _host = null;
            _downloads = null;
            _notifications = null;
            _menus = null;
            downloads?.Retire();
            if (closing) downloads?.Dispose();
            host.Diagnostic -= Diagnostic;
            if (_contextMenu != null && ReferenceEquals(_browser.MenuHandler, _contextMenu))
                _browser.MenuHandler = _contextMenu.Inner;
            _contextMenu = null;
            try { await host.DetachAsync(CancellationToken.None); }
            finally
            {
                try { if (notifications != null) await notifications.StopAsync(); }
                finally
                {
                    menus?.Dispose();
                    if (!closing && downloads != null && downloads.ActiveCount != 0)
                    {
                        var retired = new RetiredHost { Host = host, Downloads = downloads };
                        _retiredHosts.Add(retired);
                        retired.Drain = DrainRetiredHostAsync(retired);
                    }
                    else
                    {
                        downloads?.Dispose();
                        if (downloads != null) await downloads.DrainAsync();
                        host.Dispose();
                    }
                }
            }
        }

        private async Task DrainRetiredHostAsync(RetiredHost retired)
        {
            try
            {
                await retired.Downloads.DrainAsync();
                retired.Host.Dispose();
            }
            catch (Exception) { Log(LogLevel.Error, "Retired userscript host cleanup failed for tab " + _model.BrowserId); }
            finally { _retiredHosts.Remove(retired); }
        }

        private sealed class RetiredHost
        {
            public CefSharpUserScriptHost Host;
            public UserScriptDownloadService Downloads;
            public Task Drain;
        }

        private void AudioStateChanged(object sender, TabAudioState state)
        {
            if (_lifetime.IsCancellationRequested || _dispatcher.HasShutdownStarted) return;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_lifetime.IsCancellationRequested && !_model.IsClosing)
                    _model.UpdateMuteState(state.IsMuted);
            }));
        }

        private async void AddressChanged(object sender, AddressChangedEventArgs e)
        {
            try { await UpdateMatchCountAsync(e.Address); }
            catch (Exception) when (_lifetime.IsCancellationRequested) { }
            catch (Exception) { Log(LogLevel.Warning, "Userscript match status could not update for tab " + _model.BrowserId); }
        }

        private void RepositoryChanged(object sender, UserScriptRepositoryChangedEventArgs e)
        {
            if (!_lifetime.IsCancellationRequested && !_dispatcher.HasShutdownStarted)
                _dispatcher.BeginInvoke(new Action(() => AddressChanged(this, new AddressChangedEventArgs(null, _model.Address))));
        }

        private async Task UpdateMatchCountAsync(string address)
        {
            if (_lifetime.IsCancellationRequested)
                return;
            var count = 0;
            if (_host != null && Uri.TryCreate(address, UriKind.Absolute, out var url) && AllowsScripts(address))
            {
                var scripts = await _service.Repository.GetSnapshotAsync(_lifetime.Token);
                count = scripts.Count(script => script.IsEnabled && script.Definition.ParseResult.CanEnable
                    && UserScriptExecutionPolicy.IsWorldAvailable(script.Definition.Metadata, GlobalData.Settings.TrustedUserScriptPageWorld)
                    && script.PreparedRevision.UrlRules.AllowsEnvironment(false, out _)
                    && script.PreparedRevision.UrlRules.IsMatch(url));
            }
            if (!_lifetime.IsCancellationRequested)
                _model.UserScriptMatchCount = count;
        }

        private static bool AllowsScripts(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var url))
                return false;
            if (url.Scheme == "about")
                return address == "about:blank";
            if (url.Scheme != "http" && url.Scheme != "https" && url.Scheme != "file")
                return false;
            return !string.Equals(url.AbsoluteUri, new Uri(GlobalData.SwfPlayerPath).AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        }

        private void SetState(UserScriptState state, string error = null)
        {
            _model.UserScriptState = state;
            _model.UserScriptError = error;
            _model.UserScriptsUnavailable = state == UserScriptState.Unavailable;
            if (state != UserScriptState.Ready)
                _model.UserScriptMatchCount = 0;
        }

        private void Diagnostic(object sender, UserScriptDiagnostic e)
        {
            Log(LogLevel.Info, "Userscript " + e.Code + " tab=" + _model.BrowserId + " script=" + e.ScriptKey
                + " document=" + e.DocumentId + " frame=" + e.FrameId);
        }

        private void LogScript(UserScriptLogEntry entry)
        {
            lock (_logLock)
            {
                var minute = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute;
                _logCounts.TryGetValue(entry.ScriptKey, out var count);
                var next = count != null && count.Item1 == minute ? count.Item2 + 1 : 1;
                _logCounts[entry.ScriptKey] = Tuple.Create(minute, next);
                if (next > 20) return;
            }
            var value = entry.JsonValue ?? string.Empty;
            Log(LogLevel.Info, "GM.log tab=" + _model.BrowserId + " script=" + entry.ScriptKey + " "
                + (value.Length > 4096 ? value.Substring(0, 4096) : value));
        }

        private void Log(LogLevel level, string message) { try { _logger.Log(level, message); } catch { } }

        private async Task WaitForNativeAsync(CancellationToken token)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                while (!_browser.IsBrowserInitialized)
                {
                    if (_browser.IsDisposed) throw new ObjectDisposedException(nameof(_browser));
                    await Task.Delay(10, deadline.Token);
                }
            }
        }

        private static async Task WaitAsync(Task work, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetCanceled()))
            {
                await Task.WhenAny(work, cancelled.Task);
                token.ThrowIfCancellationRequested();
                await work;
            }
        }
    }
}
