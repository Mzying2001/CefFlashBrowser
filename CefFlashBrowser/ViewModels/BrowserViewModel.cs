using CefFlashBrowser.Data;
using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.Utils;
using CefSharp;
using SimpleMvvm;
using SimpleMvvm.Messaging;
using System;
using System.Collections.ObjectModel;

namespace CefFlashBrowser.ViewModels
{
    public class BrowserViewModel : ViewModelBase
    {
        public string BrowserId { get; } = Guid.NewGuid().ToString("D");

        private IWebBrowser _browser;
        public IWebBrowser Browser
        {
            get => _browser;
            set
            {
                if (ReferenceEquals(_browser, value))
                    return;

                if (_browser == null)
                {
                    UpdateValue(ref _browser, value);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Browser property can only be set once.");
                }
            }
        }


        private volatile bool _isClosed;
        public bool IsClosed => _isClosed;


        public event EventHandler Closed;


        public bool IsClosing { get; internal set; }
        public bool HasDocument { get; internal set; }


        public ObservableCollection<UserScriptMenuCommand> UserScriptMenus { get; }


        private UserScriptState _userScriptState;
        public UserScriptState UserScriptState
        {
            get => _userScriptState;
            internal set => UpdateValue(ref _userScriptState, value);
        }

        private string _userScriptError;
        public string UserScriptError
        {
            get => _userScriptError;
            internal set => UpdateValue(ref _userScriptError, value);
        }

        private bool _userScriptsUnavailable;
        public bool UserScriptsUnavailable
        {
            get => _userScriptsUnavailable;
            internal set => UpdateValue(ref _userScriptsUnavailable, value);
        }

        private int _userScriptMatchCount;
        public int UserScriptMatchCount
        {
            get => _userScriptMatchCount;
            internal set => UpdateValue(ref _userScriptMatchCount, value);
        }

        private string _address;
        public string Address
        {
            get => _address;
            set => UpdateValue(ref _address, value);
        }

        private string _title;
        public string Title
        {
            get => _title;
            set => UpdateValue(ref _title, value);
        }

        private int _muteRequestVersion;
        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            private set => UpdateValue(ref _isMuted, value);
        }

        private IntPtr _integratedDevToolsHandle = IntPtr.Zero;
        public IntPtr IntegratedDevToolsHandle
        {
            get => _integratedDevToolsHandle;
            set => UpdateValue(ref _integratedDevToolsHandle, value);
        }

        private double _zoomLevel = 0.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (UpdateValue(ref _zoomLevel, value)
                    && GlobalData.Settings.SaveZoomLevel)
                {
                    GlobalData.Settings.BrowserZoomLevel = value;
                }
            }
        }



        public BrowserViewModel()
        {
            UserScriptMenus = new ObservableCollection<UserScriptMenuCommand>();

            if (GlobalData.Settings.SaveZoomLevel)
            {
                ZoomLevel = GlobalData.Settings.BrowserZoomLevel;
            }
        }



        internal void MarkClosed()
        {
            if (_isClosed)
                return;

            _isClosed = true;

            var handlers = Closed;

            if (handlers == null)
                return;

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch { }
            }
        }

        public bool IsBrowserReady()
        {
            return !IsClosing && Browser != null && !Browser.IsDisposed && Browser.IsBrowserInitialized
                && (HasDocument || Browser.IsLoading || Browser.CanExecuteJavascriptInMainFrame);
        }

        public void OnDevToolsOpened(IntPtr hDevTools)
        {
            if (GlobalData.Settings.EnableIntegratedDevTools)
            {
                HwndHelper.ApplyEmbeddedChildStyle(hDevTools);
                IntegratedDevToolsHandle = hDevTools;
            }
            Messenger.Global.Send(MessageTokens.DEVTOOLS_OPENED, Browser);
        }

        public void OnDevToolsClosed()
        {
            IntegratedDevToolsHandle = IntPtr.Zero;
            Messenger.Global.Send(MessageTokens.DEVTOOLS_CLOSED, Browser);
        }

        public void ToggleDevTools()
        {
            if (!IsBrowserReady())
                return;

            if (IntegratedDevToolsHandle == IntPtr.Zero
                && HwndHelper.FindNotIntegratedDevTools(Browser) == IntPtr.Zero)
            {
                Browser.ShowDevTools();
            }
            else
            {
                Browser.CloseDevTools();
            }
        }

        internal void UpdateMuteState(bool isMuted)
        {
            IsMuted = isMuted;
        }

        public async void ToggleMute()
        {
            if (!IsBrowserReady())
                return;

            if (Browser is FlashBrowser.ChromiumWebBrowserEx browser)
            {
                var version = ++_muteRequestVersion;
                var previous = IsMuted;

                IsMuted = !previous;

                try
                {
                    await browser.SetAudioMutedAsync(!previous);
                    var isMuted = await browser.GetAudioMutedAsync();

                    if (version == _muteRequestVersion && !IsClosing && !IsClosed)
                        IsMuted = isMuted;
                }
                catch (ObjectDisposedException)
                {
                    if (version == _muteRequestVersion && !IsClosing && !IsClosed)
                        IsMuted = previous;
                }
                catch (InvalidOperationException)
                {
                    if (version == _muteRequestVersion && !IsClosing && !IsClosed)
                        IsMuted = previous;
                }
            }
        }

        public void ExitFullScreen()
        {
            if (!IsBrowserReady())
                return;

            try
            {
                if (Browser.CanExecuteJavascriptInMainFrame)
                {
                    Browser.ExecuteScriptAsync("if (document.fullscreenElement) document.exitFullscreen();");
                }
            }
            catch (ObjectDisposedException)
            {
                // The browser may be disposed after IsBrowserReady returns.
            }
        }

        public void FindText(string searchText, bool forward, bool matchCase, bool findNext)
        {
            try
            {
                if (IsBrowserReady())
                    Browser.GetBrowser()?.Find(0, searchText, forward, matchCase, findNext);
            }
            catch (ObjectDisposedException)
            {
                // A tab can finish closing between the readiness check and the call.
            }
        }

        public void StopFinding(bool clearSelection)
        {
            try
            {
                if (IsBrowserReady())
                    Browser.GetBrowser()?.StopFinding(clearSelection);
            }
            catch (ObjectDisposedException)
            {
                // Selection may change while CEF finishes closing the previous tab.
            }
        }
    }
}
