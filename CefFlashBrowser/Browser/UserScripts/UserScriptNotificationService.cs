using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Views.Dialogs;
using Mzying2001.MonkeySharp.Core.Apis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptNotificationService : INotificationService
    {
        private readonly object _lock = new object();
        private readonly HashSet<NotificationHandle> _handles = new HashSet<NotificationHandle>();
        private readonly Dictionary<string, NotificationHandle> _tags = new Dictionary<string, NotificationHandle>();
        private readonly Dispatcher _dispatcher;
        private readonly Func<Window> _owner;
        private readonly UserScriptTabService _tabs;
        private readonly UserScriptContentService _content;
        private readonly CancellationToken _lifetime;
        private readonly Action _report;
        private bool _stopping;
        private Task _stop;

        public UserScriptNotificationService(Dispatcher dispatcher, Func<Window> owner, UserScriptTabService tabs,
            UserScriptContentService content, CancellationToken lifetime, Action report = null)
        {
            _dispatcher = dispatcher;
            _owner = owner;
            _tabs = tabs;
            _content = content;
            _lifetime = lifetime;
            _report = report;
        }

        public INotificationHandle Create(UserScriptNotificationRequest request, CancellationToken token)
        {
            lock (_lock)
            {
                if (_stopping)
                    throw new ObjectDisposedException(nameof(UserScriptNotificationService));

                var handle = new NotificationHandle(this, request, token);
                _handles.Add(handle);
                return handle;
            }
        }

        public Task StopAsync()
        {
            _dispatcher.VerifyAccess();
            lock (_lock)
            {
                _stopping = true;
                return _stop ?? (_stop = StopCoreAsync());
            }
        }

        private async Task StopCoreAsync()
        {
            await Task.Yield();
            NotificationHandle[] handles;
            lock (_lock)
                handles = _handles.ToArray();

            foreach (var handle in handles)
                handle.Dispose();

            await Task.WhenAll(handles.Select(handle => handle.DrainAsync()));
        }

        private void RegisterTag(NotificationHandle handle, string tag)
        {
            _dispatcher.VerifyAccess();
            if (tag == null)
                return;

            NotificationHandle old;
            lock (_lock)
            {
                _tags.TryGetValue(tag, out old);
                _tags[tag] = handle;
            }

            old?.Dispose();
        }

        private async Task ReleaseAsync(NotificationHandle handle, string tag)
        {
            lock (_lock)
            {
                if (tag != null && _tags.TryGetValue(tag, out var current) && ReferenceEquals(current, handle))
                    _tags.Remove(tag);
            }

            await handle.DrainAsync();
            lock (_lock)
                _handles.Remove(handle);
        }

        private void Report()
        {
            try
            {
                _report?.Invoke();
            }
            catch { }
        }

        private sealed class NotificationHandle : INotificationHandle
        {
            private readonly UserScriptNotificationService _owner;
            private readonly UserScriptNotificationRequest _request;
            private readonly CancellationTokenSource _cancellation;
            private readonly CancellationToken _token;
            private readonly TaskCompletionSource<object> _completion = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            private CancellationTokenRegistration _registration;
            private UserScriptNotificationWindow _window;
            private DispatcherTimer _timer;
            private Task _imageTask;
            private int _started;
            private int _closed;
            private int _clicked;
            private int _resolved;

            private string Tag => string.IsNullOrEmpty(_request.Tag) ? null : _request.ScriptKey + "\n" + _request.Tag;

            public Task Completion => _completion.Task;

            public event EventHandler Clicked;
            public event EventHandler Closed;

            public NotificationHandle(UserScriptNotificationService owner, UserScriptNotificationRequest request, CancellationToken token)
            {
                _owner = owner;
                _request = request;
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, owner._lifetime);
                _token = _cancellation.Token;
            }

            public void Start()
            {
                if (Interlocked.Exchange(ref _started, 1) != 0 || Volatile.Read(ref _closed) != 0)
                    return;

                _registration = _token.Register(Dispose);
                if (Volatile.Read(ref _closed) != 0)
                    _registration.Dispose();

                if (!_owner._dispatcher.HasShutdownStarted)
                    _owner._dispatcher.BeginInvoke(new Action(Show));
                else
                    Dispose();
            }

            private async void Show()
            {
                if (Volatile.Read(ref _closed) != 0)
                    return;

                try
                {
                    _token.ThrowIfCancellationRequested();
                    _owner.RegisterTag(this, Tag);

                    if (_request.Highlight)
                        await _owner._tabs.FocusAsync(null, _token);

                    _token.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(_request.Text))
                    {
                        Finish();
                        return;
                    }

                    CreateWindow();
                    _window.Show();
                    if (Volatile.Read(ref _closed) != 0)
                        return;

                    if (!_request.Silent)
                        System.Media.SystemSounds.Asterisk.Play();

                    if (_request.Timeout > 0)
                        StartTimeout();

                    if (!string.IsNullOrEmpty(_request.ImageUrl))
                        _imageTask = LoadImageAsync();
                }
                catch (OperationCanceledException)
                {
                    Dispose();
                }
                catch (Exception e)
                {
                    _completion.TrySetException(e);
                    _owner.Report();
                    Dispose();
                }
            }

            private void CreateWindow()
            {
                _window = WindowManager.CreateWindow<UserScriptNotificationWindow>();
                _window.SetMessage(_request.Title, _request.Text);
                _window.Owner = _owner._owner();

                _window.Clicked += (sender, e) =>
                {
                    if (Interlocked.Exchange(ref _clicked, 1) == 0)
                    {
                        try
                        {
                            Clicked?.Invoke(this, EventArgs.Empty);
                        }
                        catch
                        {
                            _owner.Report();
                            Dispose();
                        }
                    }
                };
                _window.Closed += (sender, e) => Finish();
            }

            private void StartTimeout()
            {
                _timer = new DispatcherTimer(DispatcherPriority.Normal, _owner._dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(_request.Timeout.Value)
                };
                _timer.Tick += Timeout;
                _timer.Start();
            }

            private async Task LoadImageAsync()
            {
                try
                {
                    var content = await _owner._content.FetchAsync(_request.ImageUrl, false, false, _token).ConfigureAwait(false);

                    BitmapImage bitmap;
                    using (var stream = new MemoryStream(content.Bytes))
                    {
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 80;
                        bitmap.DecodePixelHeight = 80;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }

                    _token.ThrowIfCancellationRequested();
                    await _owner._dispatcher.InvokeAsync(() =>
                    {
                        if (Volatile.Read(ref _closed) == 0)
                            _window.SetImage(bitmap);
                    });
                }
                catch (OperationCanceledException) { }
                catch (Exception)
                {
                    _owner.Report();
                }
            }

            public void ResolveClick(bool preventDefault)
            {
                if (Volatile.Read(ref _clicked) == 0 || Volatile.Read(ref _closed) != 0 || Interlocked.Exchange(ref _resolved, 1) != 0)
                    return;

                _owner._dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        _token.ThrowIfCancellationRequested();
                        if (!preventDefault && _request.Url != null)
                        {
                            var tab = await _owner._tabs.OpenAsync(new OpenTabRequest(_request.Url, true, true, true), _token);
                            tab.Dispose();
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception)
                    {
                        _owner.Report();
                    }
                    finally
                    {
                        Dispose();
                    }
                }));
            }

            private void Timeout(object sender, EventArgs e) => Dispose();

            public Task DrainAsync() => _imageTask ?? Task.CompletedTask;

            private void Finish()
            {
                if (Interlocked.Exchange(ref _closed, 1) != 0)
                    return;

                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= Timeout;
                }

                _cancellation.Cancel();
                _registration.Dispose();
                try
                {
                    Closed?.Invoke(this, EventArgs.Empty);
                }
                catch
                {
                    _owner.Report();
                }

                _completion.TrySetResult(null);
                _ = ReleaseAsync();
            }

            private async Task ReleaseAsync()
            {
                await _owner.ReleaseAsync(this, Tag);
                _cancellation.Dispose();
            }

            public void Dispose()
            {
                if (_owner._dispatcher.CheckAccess())
                {
                    if (Volatile.Read(ref _closed) != 0)
                        return;

                    _window?.Close();
                    Finish();
                }
                else if (!_owner._dispatcher.HasShutdownStarted)
                    _owner._dispatcher.BeginInvoke(new Action(Dispose));
                else
                {
                    if (Interlocked.Exchange(ref _closed, 1) != 0)
                        return;

                    _cancellation.Cancel();
                    _registration.Dispose();
                    try
                    {
                        Closed?.Invoke(this, EventArgs.Empty);
                    }
                    catch
                    {
                        _owner.Report();
                    }

                    _completion.TrySetResult(null);
                    _ = ReleaseAsync();
                }
            }
        }
    }
}
