using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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
                bool hasDevTools = chromiumWebBrowser.GetBrowserHost()?.HasDevTools ?? false;
                if (hasDevTools && browser.IsPopup)
                {
                    return false;
                }

                ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
                {
                    window._doClose = true;
                    window.Close();
                });
                return false;
            }

            public override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
                {
                    if (targetDisposition == WindowOpenDisposition.NewPopup)
                    {
                        window.SetCurrentValue(FullScreenProperty, false);
                        WindowManager.ShowPopupWebPage(targetUrl, popupFeatures);
                    }
                    else
                    {
                        switch (GlobalData.Settings.NewPageBehavior)
                        {
                            case NewPageBehavior.NewWindow:
                                {
                                    window.SetCurrentValue(FullScreenProperty, false);
                                    WindowManager.ShowBrowser(targetUrl);
                                    break;
                                }
                            case NewPageBehavior.OriginalWindow:
                                {
                                    chromiumWebBrowser.Load(targetUrl);
                                    break;
                                }
                        }
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
                            ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(
                                () => window.SetCurrentValue(FullScreenProperty, false));
                            break;
                        }
                }
                return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
            }
        }



        private bool _doClose = false;
        private bool _isMaximizedBeforeFullScreen = false;

        public ICommand ToggleFullScreenCommand { get; }



        public bool FullScreen
        {
            get { return (bool)GetValue(FullScreenProperty); }
            set { SetValue(FullScreenProperty, value); }
        }

        public static readonly DependencyProperty FullScreenProperty =
            DependencyProperty.Register(nameof(FullScreen), typeof(bool), typeof(BrowserWindow), new PropertyMetadata(false, OnFullScreenChange));



        public BrowserWindow()
        {
            ToggleFullScreenCommand = new DelegateCommand(ToggleFullScreen);

            InitializeComponent();
            WindowSizeInfo.Apply(GetSizeInfo(), this);

            browser.JsDialogHandler = new Utils.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Utils.Handlers.IEDownloadHandler();
            browser.LifeSpanHandler = new BrowserLifeSpanHandler(this);
            browser.MenuHandler = new BrowserMenuHandler(this);

            BindingOperations.SetBinding(this, FullScreenProperty, new Binding
            { Source = browser, Path = new PropertyPath("FullscreenMode"), Mode = BindingMode.OneWay });

            Messenger.Global.Register(MessageTokens.DEVTOOLS_SHOWN, DevToolsShownHandler);
            Closed += delegate { Messenger.Global.Unregister(MessageTokens.DEVTOOLS_SHOWN, DevToolsShownHandler); };
        }

        private WindowSizeInfo GetSizeInfo()
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

        private void ToggleFullScreen()
        {
            if (FullScreen)
            {
                if (browser.CanExecuteJavascriptInMainFrame)
                    browser.ExecuteScriptAsync("if (document.fullscreenElement) document.exitFullscreen();");
                SetCurrentValue(FullScreenProperty, false);
            }
            else
            {
                //browser.ExecuteScriptAsync("document.documentElement.requestFullscreen();");
                SetCurrentValue(FullScreenProperty, true);
            }
        }

        private static void OnFullScreenChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            BrowserWindow window = (BrowserWindow)d;
            if ((e.OldValue != e.NewValue) && (bool)e.NewValue)
            {
                window._isMaximizedBeforeFullScreen = window.WindowState == WindowState.Maximized;
                if (window._isMaximizedBeforeFullScreen)
                    window.WindowState = WindowState.Normal;

                //window.Topmost = true;
                window.WindowStyle = WindowStyle.None;
                window.WindowState = WindowState.Maximized;
            }
            else
            {
                //window.Topmost = false;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.WindowState = WindowState.Normal;

                if (window._isMaximizedBeforeFullScreen)
                    window.WindowState = WindowState.Maximized;
            }
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
                if (!FullScreen)
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

        private void DevToolsShownHandler(object obj)
        {
            if (obj is IWebBrowser b && b == browser)
            {
                OnDevToolsShown();
            }
        }

        private void OnDevToolsShown()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == null) return;

            Win32.GetWindowThreadProcessId(hwnd, out IntPtr pid);
            if (pid == IntPtr.Zero) return;

            Task.Run(async () =>
            {
                IntPtr hDevTools = IntPtr.Zero;

                int cnt;
                for (cnt = 0; cnt < 5; cnt++)
                {
                    hDevTools = WindowManager.FindDevTools(pid);

                    if (hDevTools == IntPtr.Zero)
                        await Task.Delay(100);
                    else break;
                }

                if (hDevTools != IntPtr.Zero)
                {
                    // Set current window as the owner of the DevTools window
                    WindowManager.SetOwnerHandle(hDevTools, hwnd);
                }
                else
                {
                    LogHelper.LogInfo($"DevTools window not found after {cnt} attempts.");
                }
            });
        }
    }
}
