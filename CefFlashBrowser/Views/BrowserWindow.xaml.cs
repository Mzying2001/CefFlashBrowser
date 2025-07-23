using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefSharp;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// BrowserWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrowserWindow : Window
    {
        private class BrowserLifeSpanHandler : LifeSpanHandler
        {
            private readonly BrowserWindow window;

            public BrowserLifeSpanHandler(BrowserWindow window)
            {
                this.window = window;
            }

            public override bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                bool isPopup = browser.IsPopup;
                bool hasDevTools = chromiumWebBrowser.GetBrowserHost()?.HasDevTools ?? false;

                window.Dispatcher.Invoke(delegate
                {
                    if (isPopup && hasDevTools)
                    {
                        window.ViewModel.OnDevToolsClosed(chromiumWebBrowser);
                    }
                    else
                    {
                        window._doClose = true;
                        window.Close();
                    }
                });
                return false;
            }

            public override void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                var hwnd = browser.GetHost().GetWindowHandle();

                if (HwndHelper.IsDevToolsWindow(hwnd))
                {
                    window.Dispatcher.Invoke(() =>
                    {
                        HwndHelper.SetOwnerWindow(hwnd, new WindowInteropHelper(window).Handle);
                        window.ViewModel.OnDevToolsOpened(chromiumWebBrowser, hwnd);
                    });
                }
            }

            public override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                window.Dispatcher.Invoke(delegate
                {
                    if (targetDisposition == WindowOpenDisposition.NewPopup)
                    {
                        window.ViewModel.OnPopup(targetUrl, popupFeatures);
                    }
                    else
                    {
                        window.ViewModel.OnNewPage(targetUrl);
                    }
                });
                newBrowser = null;
                return true;
            }
        }

        private class BrowserMenuHandler : Utils.Handlers.ContextMenuHandler
        {
            private readonly BrowserWindow window;

            public BrowserMenuHandler(BrowserWindow window)
            {
                this.window = window;
            }

            public override bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
            {
                switch (commandId)
                {
                    case Search:
                    case OpenInNewWindow:
                    case OpenSelectedUrl:
                    case CefMenuCommand.ViewSource:
                        {
                            window.Dispatcher.Invoke(() => window.ViewModel.FullScreen = false);
                            break;
                        }
                }
                return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
            }
        }



        private bool _doClose = false;
        private bool _isMaximizedBeforeFullScreen = false;
        private GridLength _devToolsColumnWidth = new GridLength(0, GridUnitType.Auto);



        public BrowserWindowViewModel ViewModel
        {
            get => DataContext as BrowserWindowViewModel;
            set => DataContext = value;
        }



        public BrowserWindow()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GetSizeInfo(), this);

            browser.JsDialogHandler = new Utils.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Utils.Handlers.IEDownloadHandler();
            browser.LifeSpanHandler = new BrowserLifeSpanHandler(this);
            browser.MenuHandler = new BrowserMenuHandler(this);

            Messenger.Global.Register(MessageTokens.DEVTOOLS_OPENED, DevToolsOpenedHandler);
            Messenger.Global.Register(MessageTokens.DEVTOOLS_CLOSED, DevToolsClosedHandler);
            Messenger.Global.Register(MessageTokens.FULLSCREEN_CHANGED, FullScreenChangedHandler);

            Closed += delegate
            {
                Messenger.Global.Unregister(MessageTokens.DEVTOOLS_OPENED, DevToolsOpenedHandler);
                Messenger.Global.Unregister(MessageTokens.DEVTOOLS_CLOSED, DevToolsClosedHandler);
                Messenger.Global.Unregister(MessageTokens.FULLSCREEN_CHANGED, FullScreenChangedHandler);
            };
        }

        public WindowSizeInfo GetSizeInfo()
        {
            WindowSizeInfo info = null;

            if (WindowManager.GetLastBrowserWindow() is Window window)
            {
                info = WindowSizeInfo.GetSizeInfo(window);
                info.Left += 20;
                info.Top += 20;
            }

            return info ?? GlobalData.Settings.BrowserWindowSizeInfo;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.Escape && browser.IsLoading)
            {
                // Why not use KeyBinding: The Esc key serves other purposes in many situations, 
                // not just stopping loading. If KeyBinding is used, this would be considered as 
                // the event being handled, thus intercepting the Esc key event.
                browser.Stop();
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd).AddHook(new HwndSourceHook(WndProc));
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case Win32.WM_MOVE:
                    {
                        UpdateStatusPopupPosition();
                        break;
                    }
            }
            return IntPtr.Zero;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateStatusPopupPosition();
        }

        private void UpdateStatusPopupPosition()
        {
            var pos = PointToScreen(new Point(0, mainGrid.ActualHeight - statusPopupContent.Height));
            statusPopup.PlacementRectangle = new Rect { X = pos.X, Y = pos.Y };
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel) return;

            if (browser.IsDisposed || _doClose)
            {
                if (!ViewModel.FullScreen)
                    GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
            else
            {
                bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
                browser.GetBrowser().CloseBrowser(forceClose);
                e.Cancel = true;
            }
        }

        private void OpenBottomContextMenu(UIElement target, ContextMenu menu)
        {
            menu.IsOpen = true;
            menu.Placement = PlacementMode.Relative;
            menu.PlacementTarget = target;
            menu.PlacementRectangle = new Rect
            {
                X = target.RenderSize.Width - menu.RenderSize.Width,
                Y = target.RenderSize.Height
            };
        }

        private void MenuButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenBottomContextMenu((UIElement)sender, (ContextMenu)Resources["more"]);
        }

        private void ShowBlockedSwfsButtonClicked(object sender, RoutedEventArgs e)
        {
            OpenBottomContextMenu((UIElement)sender, (ContextMenu)Resources["blockedSwfs"]);
        }

        private void DevToolsOpenedHandler(object msg)
        {
            if (msg == browser)
            {
                OnDevToolsOpened();
            }
        }

        private void DevToolsClosedHandler(object msg)
        {
            if (msg == browser)
            {
                OnDevToolsClosed();
            }
        }

        private void OnDevToolsOpened()
        {
            if (GlobalData.Settings.EnableIntegratedDevTools)
            {
                Activate();
                Keyboard.Focus(browser);
                devtoolsColumn.Width = _devToolsColumnWidth;
            }
        }

        private void OnDevToolsClosed()
        {
            Activate();
            Keyboard.Focus(browser);
            _devToolsColumnWidth = devtoolsColumn.Width;
            devtoolsColumn.Width = new GridLength(0, GridUnitType.Auto);
        }

        private void BrowserFullscreenModeChanged(object sender, EventArgs e)
        {
            ViewModel.FullScreen = browser.FullscreenMode;
        }

        private void FullScreenChangedHandler(object msg)
        {
            if (msg == DataContext)
            {
                OnFullScreenChanged(ViewModel.FullScreen);
            }
        }

        private void OnFullScreenChanged(bool fullScreen)
        {
            if (fullScreen)
            {
                _isMaximizedBeforeFullScreen =
                    WindowState == WindowState.Maximized;

                if (_isMaximizedBeforeFullScreen)
                    WindowState = WindowState.Normal;

                //Topmost = true;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
            else
            {
                //Topmost = false;
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = WindowState.Normal;

                if (_isMaximizedBeforeFullScreen)
                    WindowState = WindowState.Maximized;
            }
        }
    }
}
