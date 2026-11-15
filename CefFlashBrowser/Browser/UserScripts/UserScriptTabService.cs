using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.Views;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptTabService : ITabService, IUserScriptWindowService
    {
        private readonly BrowserViewModel _source;
        private readonly Func<Window> _owner;
        private readonly Func<Task<bool>> _close;
        private readonly Dispatcher _dispatcher;
        private readonly CancellationToken _lifetime;
        // Dispatcher-owned reservations make the last-browser rule atomic across
        // simultaneous script calls, including while beforeunload is pending.
        private static readonly Dictionary<BrowserViewModel, Task<bool>> _pendingCloses = new Dictionary<BrowserViewModel, Task<bool>>();

        public bool SupportsIncognito => false;

        public UserScriptTabService(BrowserViewModel source, Func<Window> owner, Func<Task<bool>> close,
            Dispatcher dispatcher, CancellationToken lifetime)
        {
            _source = source;
            _owner = owner;
            _close = close;
            _dispatcher = dispatcher;
            _lifetime = lifetime;
        }

        public async Task<ITabHandle> OpenAsync(OpenTabRequest request, CancellationToken token)
        {
            if (request.Incognito == true)
                throw new NotSupportedException("Private script tabs are not supported.");

            if (!request.Url.IsAbsoluteUri || request.Url.Scheme != "http" && request.Url.Scheme != "https" && request.Url.AbsoluteUri != "about:blank")
                throw new ArgumentException("Script tabs require HTTP(S) or about:blank.", nameof(request));

            return await _dispatcher.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                _lifetime.ThrowIfCancellationRequested();
                if (_source.IsClosed || _source.IsClosing)
                    throw new OperationCanceledException("The source tab is closing.");

                var owner = _owner();
                if (IsOwnerClosing(owner))
                    throw new InvalidOperationException("The source window is closing.");

                var window = owner as BrowserWindow ?? Application.Current.Windows.OfType<BrowserWindow>()
                    .LastOrDefault(candidate => !candidate.ViewModel.IsClosing);
                BrowserViewModel tab;
                if (window == null)
                {
                    window = WindowManager.OpenBrowser(request.Url.AbsoluteUri, request.Active);
                    tab = window.ViewModel.CurrentBrowser;
                }
                else
                {
                    var opener = owner == window ? _source : null;
                    tab = window.ViewModel.OpenUserScriptTab(opener, request.Url.AbsoluteUri, request.Active,
                        request.Insert, request.SetParent, request.InsertIndex);
                }

                return (ITabHandle)new TabHandle(window, tab);
            });
        }

        public async Task<bool> CloseAsync(DocumentFrame frame, CancellationToken token)
        {
            var close = await _dispatcher.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                _lifetime.ThrowIfCancellationRequested();
                if (_source.IsClosed)
                    return Task.FromResult(true);

                if (_source.IsClosing)
                    return Task.FromResult(false);

                return ReserveClose(_source, () => _owner() is BrowserWindow window
                    ? window.CloseScriptTabAsync(_source) : _close());
            });
            // Detach cancels the calling execution during an accepted close.
            // Waiting must not make that execution block its own shutdown.
            return await WaitForCloseAsync(close, token);
        }

        public async Task<bool> FocusAsync(DocumentFrame frame, CancellationToken token)
        {
            return await _dispatcher.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                _lifetime.ThrowIfCancellationRequested();
                var owner = _owner();
                if (_source.IsClosed || _source.IsClosing || owner == null || IsOwnerClosing(owner))
                    return false;

                if (owner is BrowserWindow window)
                {
                    if (window.ViewModel.IsClosing)
                        return false;

                    window.ViewModel.CurrentBrowser = _source;
                }

                if (owner.WindowState == WindowState.Minimized)
                    owner.WindowState = WindowState.Normal;

                return owner.Activate();
            });
        }

        private static bool CanCloseBrowser()
        {
            var count = 0;
            foreach (Window window in Application.Current.Windows)
            {
                if (IsOwnerClosing(window))
                    continue;

                if (window is BrowserWindow browserWindow)
                    count += browserWindow.ViewModel.Browsers.Count(tab => !tab.IsClosed && !tab.IsClosing && !_pendingCloses.ContainsKey(tab));
                else if ((window is PopupWebPage || window is SwfPlayerWindow)
                    && window.FindName("browserView") is BrowserView view && !view.ViewModel.IsClosed && !view.ViewModel.IsClosing
                    && !_pendingCloses.ContainsKey(view.ViewModel))
                    count++;
            }

            return count > 1;
        }

        private static bool IsOwnerClosing(Window owner)
        {
            return owner is BrowserWindow window && window.ViewModel.IsClosing
                || owner is PopupWebPage popup && popup.IsClosing
                || owner is SwfPlayerWindow player && player.IsClosing;
        }

        private static Task<bool> ReserveClose(BrowserViewModel tab, Func<Task<bool>> close)
        {
            if (tab.IsClosed)
                return Task.FromResult(true);

            if (_pendingCloses.TryGetValue(tab, out var pending))
                return pending;

            if (!CanCloseBrowser())
                return Task.FromResult(false);

            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingCloses.Add(tab, completion.Task);
            _ = CompleteReservedCloseAsync(tab, close, completion);
            return completion.Task;
        }

        private static async Task CompleteReservedCloseAsync(BrowserViewModel tab, Func<Task<bool>> close, TaskCompletionSource<bool> completion)
        {
            try
            {
                completion.TrySetResult(await close());
            }
            catch (Exception e)
            {
                completion.TrySetException(e);
            }
            finally
            {
                _pendingCloses.Remove(tab);
            }
        }

        private static async Task<bool> WaitForCloseAsync(Task<bool> work, CancellationToken token)
        {
            var cancellation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancellation.TrySetCanceled()))
            {
                await Task.WhenAny(work, cancellation.Task);
                token.ThrowIfCancellationRequested();
                return await work;
            }
        }

        private sealed class TabHandle : ITabHandle
        {
            private readonly BrowserWindow _window;
            private readonly BrowserViewModel _tab;
            private int _disposed;

            public string TabId => _tab.BrowserId;

            public bool Closed => _tab.IsClosed;

            public event EventHandler OnClose;

            public TabHandle(BrowserWindow window, BrowserViewModel tab)
            {
                _window = window;
                _tab = tab;
                _tab.Closed += TabClosed;
            }

            private void TabClosed(object sender, EventArgs e) => OnClose?.Invoke(this, EventArgs.Empty);

            public async Task CloseAsync(CancellationToken token)
            {
                var close = await _window.Dispatcher.InvokeAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (Closed)
                        return Task.FromResult(true);

                    return ReserveClose(_tab, () => _window.CloseScriptTabAsync(_tab));
                });
                await WaitForCloseAsync(close, token);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                    _tab.Closed -= TabClosed;
            }
        }
    }
}
