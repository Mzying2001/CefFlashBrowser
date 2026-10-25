using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefSharp;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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

        private bool _closingWindow;
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

            Messenger.Global.Register(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
            Messenger.Global.Register(MessageTokens.CLOSE_BROWSER, CloseTabHandler);
            Messenger.Global.Register(MessageTokens.FOCUS_FIND_POPUP, FocusFindPopupHandler);
            Messenger.Global.Register(MessageTokens.FULLSCREEN_CHANGED, FullScreenChangedHandler);

            Closed += delegate
            {
                Messenger.Global.Unregister(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
                Messenger.Global.Unregister(MessageTokens.CLOSE_BROWSER, CloseTabHandler);
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

        private void TabSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Removing the selected item temporarily clears WPF's selection.
            // The view model chooses its neighbor once collection removal finishes.
            if (tabList.SelectedItem is BrowserViewModel browser)
            {
                ViewModel.CurrentBrowser = browser;
                tabList.ScrollIntoView(browser);

                if (browser.Address == "about:blank")
                {
                    addressTextBox.Focus();
                    addressTextBox.SelectAll();
                }
            }
        }

        private void TabPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && sender is ListBoxItem item)
            {
                e.Handled = true;
                ViewModel.CloseBrowser(item.DataContext as BrowserViewModel);
            }
        }

        private void TabListPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (tabList.Template.FindName("tabScrollViewer", tabList) is ScrollViewer scrollViewer)
            {
                if (e.Delta > 0)
                    scrollViewer.LineLeft();
                else if (e.Delta < 0)
                    scrollViewer.LineRight();

                e.Handled = true;
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
            if (_doClose || ViewModel == null || ViewModel.Browsers.All(x => x.Browser == null || x.Browser.IsDisposed))
            {
                if (ViewModel?.Fullscreen != true)
                    GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
            else
            {
                e.Cancel = true;
                if (!_closingWindow)
                {
                    _closingWindow = true;
                    ViewModel.IsClosing = true;
                    // Leave WPF's Closing event before removing tabs or closing again.
                    Dispatcher.InvokeAsync(CloseBrowsers);
                }
            }
            base.OnClosing(e);
        }

        private async void CloseBrowsers()
        {
            try
            {
                var browsers = ViewModel.Browsers.OrderBy(x => x == ViewModel.CurrentBrowser ? 0 : 1).ToArray();
                foreach (var browser in browsers)
                {
                    if (_isClosed || _forceClosePending)
                        return;

                    if (!ViewModel.Browsers.Contains(browser))
                        continue;

                    ViewModel.CurrentBrowser = browser;
                    if (!await CloseTabAsync(browser))
                        return;
                }

                if (!_isClosed && !_forceClosePending)
                {
                    _doClose = true;
                    Close();
                }
            }
            finally
            {
                if (!_isClosed && !_forceClosePending)
                {
                    _closingWindow = false;
                    ViewModel.IsClosing = false;
                }
            }
        }

        private async void CloseTabHandler(object msg)
        {
            if (!_isClosed && !_closingWindow && !_forceClosePending
                && msg is BrowserViewModel browser && ViewModel.Browsers.Contains(browser))
            {
                await CloseTabAsync(browser);
            }
        }

        private BrowserView FindBrowserView(DependencyObject element, BrowserViewModel browser)
        {
            if (element is BrowserView view && ReferenceEquals(view.ViewModel, browser))
                return view;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = FindBrowserView(VisualTreeHelper.GetChild(element, i), browser);
                if (child != null) return child;
            }
            return null;
        }

        private async Task<bool> CloseTabAsync(BrowserViewModel browser, BrowserView approvedView = null, bool forceClose = false)
        {
            if (_isClosed || !ViewModel.Browsers.Contains(browser))
                return true;

            var view = approvedView
                ?? FindBrowserView(browsersControl, browser);

            if (view != null)
            {
                bool closed = await (approvedView != null ? view.CompleteCloseAsync() : view.RequestCloseAsync(forceClose));
                if (!closed) return false;
            }

            if (!_isClosed)
            {
                ViewModel.RemoveBrowser(browser);

                if (ViewModel.Browsers.Count == 0
                    && !_closingWindow && !_forceClosePending)
                {
                    _doClose = true;
                    Close();
                }
            }
            return true;
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;

            if (ViewModel != null)
            {
                ViewModel.IsClosing = true;

                foreach (var browser in ViewModel.Browsers.ToArray())
                {
                    FindBrowserView(browsersControl, browser)?.Dispose();
                    ViewModel.RemoveBrowser(browser);
                }
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

            _forceClosePending = true;

            if (ViewModel != null)
                ViewModel.IsClosing = true;

            try
            {
                var closing = ForceCloseBrowsersAsync();
                if (!closing.IsCompleted)
                {
                    // Keep all native parents alive during creation and close callbacks.
                    // Callers can shut CEF down as soon as this synchronous method returns.
                    var frame = new DispatcherFrame();
                    closing.GetAwaiter().OnCompleted(() => Dispatcher.InvokeAsync(() => frame.Continue = false));
                    Dispatcher.PushFrame(frame);
                }

                closing.GetAwaiter().GetResult();
            }
            finally
            {
                _forceClosePending = false;
            }
        }

        private async Task ForceCloseBrowsersAsync()
        {
            if (ViewModel != null)
            {
                await Task.WhenAll(ViewModel.Browsers.ToArray()
                    .Select(browser => CloseTabAsync(browser, forceClose: true)));
            }

            if (_isClosed)
                return;

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
            if (IsCurrentBrowserEvent(e) && GlobalData.Settings.EnableIntegratedDevTools)
            {
                Activate();
            }
        }

        private void DevToolsClosed(object sender, RoutedEventArgs e)
        {
            if (IsCurrentBrowserEvent(e))
            {
                Activate();
            }
        }

        private void FullscreenChanged(object sender, FullscreenChangedEventArgs e)
        {
            if (IsCurrentBrowserEvent(e))
            {
                ViewModel.Fullscreen = e.Fullscreen;
            }
        }

        private bool IsCurrentBrowserEvent(RoutedEventArgs e)
        {
            return !_isClosed && e.OriginalSource is BrowserView view
                && ReferenceEquals(view.ViewModel, ViewModel.CurrentBrowser);
        }

        private async void BrowserDoClose(object sender, BrowserDoCloseEventArgs e)
        {
            if (!_isClosed && !e.IsPopup && !e.IsDevTools
                && e.OriginalSource is BrowserView view && ViewModel.Browsers.Contains(view.ViewModel)
                && ReferenceEquals(view.ViewModel.Browser, e.ChromiumWebBrowser))
            {
                e.Handled = true;
                await CloseTabAsync(view.ViewModel, view);
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
            if (!_isClosed && e.OriginalSource is BrowserView view)
            {
                ViewModel.OnPopup(view.ViewModel, e.TargetUrl, e.PopupSizeInfo);
            }
        }

        private void BrowserNewPage(object sender, BrowserNewPageEventArgs e)
        {
            if (!_isClosed && e.OriginalSource is BrowserView view)
            {
                ViewModel.OnNewPage(view.ViewModel, e.TargetUrl,
                    e.TargetDisposition != WindowOpenDisposition.NewBackgroundTab, e.BehaviorOverride);
            }
        }
    }
}
