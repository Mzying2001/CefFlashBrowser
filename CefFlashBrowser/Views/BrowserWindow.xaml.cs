using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// BrowserWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrowserWindow : Window
    {
        private bool _doClose = false;
        private bool _isClosed = false;
        private ChromiumWebBrowser _browserPendingClose;
        private bool _forceClosePending;

        private IntPtr _hwnd;
        private HwndSource _hwndSource;

        private WindowState _windowStateBeforeFullScreen;
        private bool _windowStateRestorePending = false;



        public BrowserWindowViewModel ViewModel
        {
            get => DataContext as BrowserWindowViewModel;
            set => DataContext = value;
        }



        public BrowserWindow()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GetSizeInfo(), this);

            if (GlobalData.Settings.DisableBrowserShortcuts)
            {
                InputBindings.Clear();
            }

            ViewModel = SimpleIoc.Global.GetInstance<BrowserWindowViewModel>();

            Messenger.Global.Register(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
            Messenger.Global.Register(MessageTokens.FOCUS_FIND_POPUP, FocusFindPopupHandler);
            Messenger.Global.Register(MessageTokens.FULLSCREEN_CHANGED, FullScreenChangedHandler);

            Closed += delegate
            {
                Messenger.Global.Unregister(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
                Messenger.Global.Unregister(MessageTokens.FOCUS_FIND_POPUP, FocusFindPopupHandler);
                Messenger.Global.Unregister(MessageTokens.FULLSCREEN_CHANGED, FullScreenChangedHandler);
            };

            // IsActive may not be settled during SourceInitialized, so initialize the
            // status popup visibility after the window has loaded.
            Loaded += delegate { UpdateStatusPopupVisibility(); };
        }

        public WindowSizeInfo GetSizeInfo()
        {
            WindowSizeInfo info = null;

            if (WindowManager.GetCurrentBrowserWindow() is Window w)
            {
                info = WindowSizeInfo.GetSizeInfo(w);
                info.Left += 20;
                info.Top += 20;
            }

            return info ?? GlobalData.Settings.BrowserWindowSizeInfo;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape)
            {
                // Why not use KeyBinding: The Esc key serves other purposes in many situations, 
                // not just stopping loading. If KeyBinding is used, this would be considered as 
                // the event being handled, thus intercepting the Esc key event.
                if (ViewModel.ShowFindPopup)
                {
                    ViewModel.ShowFindPopup = false;
                }
                else if (ViewModel.CurrentBrowser?.Browser is IWebBrowser browser
                    && !browser.IsDisposed && browser.IsLoading)
                {
                    browser.Stop();
                }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            _hwndSource.AddHook(new HwndSourceHook(WndProc));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case Win32.WM_MOVE:
                    {
                        UpdateStatusPopupPosition();
                        UpdateFindPopupPosition();
                        break;
                    }
            }
            return IntPtr.Zero;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateStatusPopupPosition();
            UpdateFindPopupPosition();
        }

        private void UpdateStatusPopupPosition()
        {
            var pos = PointToScreen(new Point(0, mainGrid.ActualHeight - statusPopupContent.Height));
            statusPopup.PlacementRectangle = new Rect { X = pos.X, Y = pos.Y };
        }

        private void UpdateStatusPopupVisibility()
        {
            statusPopup.SetCurrentValue(Popup.IsOpenProperty, IsActive && WindowState != WindowState.Minimized);
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            UpdateStatusPopupVisibility();
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            UpdateStatusPopupVisibility();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (WindowState == WindowState.Minimized)
            {
                ViewModel.ShowFindPopup = false;
            }
            else if (_windowStateRestorePending)
            {
                _windowStateRestorePending = false;
                WindowState = _windowStateBeforeFullScreen;
            }
            UpdateStatusPopupVisibility();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var browser = ViewModel?.CurrentBrowser?.Browser;

            if (_doClose || browser == null || browser.IsDisposed)
            {
                if (ViewModel?.Fullscreen != true)
                    GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
            else if (browser is ChromiumWebBrowser wpfBrowser && !browser.IsBrowserInitialized)
            {
                DeferCloseUntilBrowserInitialized(wpfBrowser);
                e.Cancel = true;
            }
            else
            {
                bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
                var cefBrowser = browser.GetBrowser();

                if (cefBrowser != null)
                {
                    cefBrowser.CloseBrowser(forceClose);
                    e.Cancel = true;
                }
                else
                {
                    _doClose = true;
                }
            }
            base.OnClosing(e);
        }

        private void DeferCloseUntilBrowserInitialized(ChromiumWebBrowser browser)
        {
            if (_browserPendingClose == browser)
                return;

            _browserPendingClose = browser;
            browser.IsBrowserInitializedChanged += BrowserInitializedForClose;
        }

        private void BrowserInitializedForClose(object sender, EventArgs e)
        {
            var browser = _browserPendingClose;
            if (browser == null || !browser.IsBrowserInitialized)
                return;

            browser.IsBrowserInitializedChanged -= BrowserInitializedForClose;
            _browserPendingClose = null;
            if (_forceClosePending)
                return;

            // The initialized notification can reach WPF before CEF has returned
            // from OnAfterCreated. Wait for that callback before destroying its HWND.
            Cef.UIThreadTaskFactory.StartNew(() => Dispatcher.InvokeAsync(() =>
            {
                if (_isClosed)
                    return;

                Close();
            }));
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            if (_browserPendingClose != null)
            {
                _browserPendingClose.IsBrowserInitializedChanged -= BrowserInitializedForClose;
                _browserPendingClose = null;
            }
            base.OnClosed(e);
            _hwndSource?.RemoveHook(WndProc);
        }

        private void FullScreenChangedHandler(object msg)
        {
            if (msg == ViewModel)
            {
                OnFullScreenChanged(ViewModel.Fullscreen);
            }
        }

        private void FocusFindPopupHandler(object msg)
        {
            if (msg == DataContext)
            {
                // Re-focus the search textbox when Ctrl+F is pressed while find popup is already open
                Keyboard.Focus(findTextBox);
            }
        }

        private void OnFullScreenChanged(bool fullScreen)
        {
            if (GlobalData.Settings.DisableFullscreen)
            {
                if (WindowStyle == WindowStyle.None)
                    WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else
            {
                if (fullScreen)
                {
                    _windowStateBeforeFullScreen = WindowState;

                    if (WindowState == WindowState.Maximized)
                        WindowState = WindowState.Normal;

                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;

                    if (WindowState == WindowState.Minimized)
                    {
                        _windowStateRestorePending = true;
                    }
                    else
                    {
                        _windowStateRestorePending = false;
                        WindowState = _windowStateBeforeFullScreen;
                    }
                }
            }
            UpdateFindPopupPosition();
        }

        public void ExitFullScreen()
        {
            ViewModel.Fullscreen = false;
        }

        private void CloseBrowserHandler(object msg)
        {
            ForceCloseWindow();
        }

        public void ForceCloseWindow()
        {
            if (_isClosed || _forceClosePending)
                return;

            var browser = ViewModel?.CurrentBrowser?.Browser;
            if (browser != null && !browser.IsDisposed)
            {
                // Keep the native parent alive until asynchronous CEF creation finishes.
                // Pump WPF so a caller can safely shut CEF down as soon as this returns.
                _forceClosePending = true;
                browser.LifeSpanHandler = null;
                var frame = new DispatcherFrame();
                EventHandler closed = (s, e) => frame.Continue = false;
                Closed += closed;
                try
                {
                    Cef.UIThreadTaskFactory.StartNew(() => Dispatcher.InvokeAsync(() =>
                    {
                        if (!_isClosed)
                        {
                            _doClose = true;
                            if (!browser.IsDisposed && browser.IsBrowserInitialized)
                                browser.GetBrowser()?.CloseBrowser(true);
                            Close();
                        }
                    }));
                    Dispatcher.PushFrame(frame);
                }
                finally
                {
                    Closed -= closed;
                    _forceClosePending = false;
                }
                return;
            }

            _doClose = true;
            Close();
        }

        private void StatusPopupMouseEnter(object sender, MouseEventArgs e)
        {
            UpdateStatusPopupOffset();
        }

        private void StatusPopupOpened(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(statusPopup.Child) is HwndSource hwndSource)
            {
                var hPopup = hwndSource.Handle;
                HwndHelper.SetOwnerWindow(hPopup, _hwnd);
                HwndHelper.SetWindowTopmost(hPopup, false);
            }
        }

        private void StatusPopupSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateStatusPopupOffset();
        }

        private void UpdateStatusPopupOffset()
        {
            if (!statusPopupContent.IsLoaded)
                return;

            Rect popupRect = new Rect(
                statusPopupContent.PointToScreen(new Point(0, -statusPopup.VerticalOffset)),
                statusPopupContent.PointToScreen(new Point(statusPopupContent.ActualWidth, statusPopupContent.ActualHeight)));

            Win32.GetCursorPos(out var cursorPos);
            statusPopup.VerticalOffset = (cursorPos.y >= popupRect.Y && cursorPos.y <= popupRect.Bottom) ? -30 : 0;
        }

        private void FindPopupOpened(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(findPopup.Child) is HwndSource hwndSource)
            {
                var hPopup = hwndSource.Handle;
                HwndHelper.SetOwnerWindow(hPopup, _hwnd);
                HwndHelper.SetWindowTopmost(hPopup, false);

                var exStyle = HwndHelper.GetWindowExStyle(hPopup);
                HwndHelper.SetWindowExStyle(hPopup, exStyle & ~Win32.WS_EX_NOACTIVATE);
            }
            Keyboard.Focus(findTextBox);
            UpdateFindPopupPosition();
        }

        private void FindPopupClosed(object sender, EventArgs e)
        {
            var browser = ViewModel?.CurrentBrowser?.Browser;
            if (browser != null && !browser.IsDisposed && browser.IsBrowserInitialized)
                browser.GetBrowser()?.StopFinding(true);
        }

        private void UpdateFindPopupPosition()
        {
            Point pos;
            var popupWidth = findPopup.Child.RenderSize.Width;

            if (findButton.IsVisible)
            {
                pos = findButton.PointToScreen(new Point
                {
                    X = findButton.ActualWidth - popupWidth,
                    Y = findButton.ActualHeight
                });
            }
            else
            {
                pos = mainGrid.PointToScreen(new Point
                {
                    X = mainGrid.ActualWidth - popupWidth - 20,
                    Y = 20
                });
            }

            // Clamp to current monitor's work area to prevent WPF popup flip
            var hMonitor = Win32.MonitorFromWindow(_hwnd, Win32.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new Win32.MONITORINFO { cbSize = Marshal.SizeOf<Win32.MONITORINFO>() };

            if (Win32.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                var workArea = monitorInfo.rcWork;

                if (pos.X + popupWidth > workArea.right)
                    pos.X = workArea.right - popupWidth;

                if (pos.X < workArea.left)
                    pos.X = workArea.left;
            }

            findPopup.PlacementRectangle = new Rect { X = pos.X, Y = pos.Y };
        }

        private void DevToolsOpened(object sender, RoutedEventArgs e)
        {
            if (!_isClosed && GlobalData.Settings.EnableIntegratedDevTools)
                Activate();
        }

        private void DevToolsClosed(object sender, RoutedEventArgs e)
        {
            if (!_isClosed)
                Activate();
        }

        private void FullscreenChanged(object sender, FullscreenChangedEventArgs e)
        {
            if (!_isClosed)
                ViewModel.Fullscreen = e.Fullscreen;
        }

        private void BrowserDoClose(object sender, BrowserDoCloseEventArgs e)
        {
            if (!_isClosed && !e.IsPopup && !e.IsDevTools)
            {
                _doClose = true;
                // Return from CEF's DoClose callback before destroying its parent HWND.
                Cef.UIThreadTaskFactory.StartNew(() => Dispatcher.InvokeAsync(() =>
                {
                    if (!_isClosed)
                        Close();
                }));
            }
        }

        private void BrowserAfterCreated(object sender, BrowserAfterCreatedEventArgs e)
        {
            if (!_isClosed && HwndHelper.IsDevToolsWindow(e.HostHandle))
            {
                HwndHelper.SetOwnerWindow(e.HostHandle, _hwnd);
            }
        }

        private void BrowserPopup(object sender, BrowserPopupEventArgs e)
        {
            if (!_isClosed)
                ViewModel.OnPopup(e.TargetUrl, e.PopupSizeInfo);
        }

        private void BrowserNewPage(object sender, BrowserNewPageEventArgs e)
        {
            if (!_isClosed)
                ViewModel.OnNewPage(e.TargetUrl);
        }
    }
}
