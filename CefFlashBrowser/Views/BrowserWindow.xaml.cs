using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// BrowserWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrowserWindow : Window
    {
        private class BrowserKeyboardHandler : KeyboardHandler
        {
            private readonly BrowserWindow window;
            private static readonly ViewModels.BrowserWindowViewModel viewModel;

            static BrowserKeyboardHandler()
            {
                viewModel = ((ViewModels.ViewModelLocator)Application.Current.Resources["Locator"]).BrowserWindowViewModel;
            }

            public BrowserKeyboardHandler(BrowserWindow window)
            {
                this.window = window;
            }

            public override bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
            {
                if (type != KeyType.KeyUp)
                {
                    return false;
                }
                var result = false;
                ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
                {
                    if (modifiers == CefEventFlags.None)
                    {
                        switch (windowsKeyCode)
                        {
                            case Win32.VirtualKeys.VK_ESCAPE: //Esc
                                {
                                    browser.StopLoad();
                                    result = true;
                                    break;
                                }
                            case Win32.VirtualKeys.VK_F5: //F5
                                {
                                    browser.Reload();
                                    result = true;
                                    break;
                                }
                            case Win32.VirtualKeys.VK_F11: //F11
                                {
                                    window.FullScreen = !window.FullScreen;
                                    break;
                                }
                            case Win32.VirtualKeys.VK_F12: //F12
                                {
                                    window.FullScreen = false;
                                    viewModel.ShowDevTools(chromiumWebBrowser);
                                    result = true;
                                    break;
                                }
                        }
                    }
                    else if (modifiers == CefEventFlags.ControlDown)
                    {
                        switch (windowsKeyCode)
                        {
                            case '0': //Ctrl+0
                                {
                                    ((ChromiumWebBrowser)chromiumWebBrowser).ZoomReset();
                                    result = true;
                                    break;
                                }
                            case 'D': //Ctrl+D
                                {
                                    viewModel.AddFavorite(chromiumWebBrowser);
                                    result = true;
                                    break;
                                }
                            case 'M': //Ctrl+M
                                {
                                    viewModel.ShowMainWindow();
                                    result = true;
                                    break;
                                }
                            case 'O': //Ctrl+O
                                {
                                    window.FullScreen = false;
                                    viewModel.OpenInDefaultBrowser(chromiumWebBrowser.Address);
                                    result = true;
                                    break;
                                }
                            case 'P': //Ctrl+P
                                {
                                    browser.Print();
                                    result = true;
                                    break;
                                }
                            case 'U': //Ctrl+U
                                {
                                    window.FullScreen = false;
                                    viewModel.ViewSource(chromiumWebBrowser.Address);
                                    result = true;
                                    break;
                                }
                            case 'S': //Ctrl+S
                                {
                                    viewModel.CreateShortcut(chromiumWebBrowser);
                                    result = true;
                                    break;
                                }
                            case 'W': //Ctrl+W
                                {
                                    viewModel.CloseBrowser(chromiumWebBrowser);
                                    result = true;
                                    break;
                                }
                        }
                    }
                    else if (modifiers == CefEventFlags.AltDown)
                    {
                        switch (windowsKeyCode)
                        {
                            case Win32.VirtualKeys.VK_LEFT: //Alt+Left
                                {
                                    browser.GoBack();
                                    result = true;
                                    break;
                                }
                            case Win32.VirtualKeys.VK_RIGHT: //Alt+Right
                                {
                                    browser.GoForward();
                                    result = true;
                                    break;
                                }
                        }
                    }
                });
                return result;
            }
        }

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
                        window.FullScreen = false;
                        WindowManager.ShowPopupWebPage(targetUrl, popupFeatures);
                    }
                    else
                    {
                        switch (GlobalData.Settings.NewPageBehavior)
                        {
                            case NewPageBehavior.NewWindow:
                                {
                                    window.FullScreen = false;
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

        private class BrowserDisplayHandler : DisplayHandler
        {
            private readonly BrowserWindow window;

            public BrowserDisplayHandler(BrowserWindow window)
            {
                this.window = window;
            }

            public override void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
            {
                ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(() => window.FullScreen = fullscreen);
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
                    case CefMenuCommand.ViewSource:
                        {
                            ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(() => window.FullScreen = false);
                            break;
                        }
                }
                return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
            }
        }


        private bool _doClose = false;
        private bool _isMaximizedBeforeFullScreen = false;


        public bool FullScreen
        {
            get { return (bool)GetValue(FullScreenProperty); }
            set { SetValue(FullScreenProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FullScreen.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FullScreenProperty =
            DependencyProperty.Register("FullScreen", typeof(bool), typeof(BrowserWindow), new PropertyMetadata(false, OnFullScreenChange));


        public BrowserWindow()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GlobalData.Settings.BrowserWindowSizeInfo, this);

            browser.JsDialogHandler = new Utils.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Utils.Handlers.IEDownloadHandler();
            browser.KeyboardHandler = new BrowserKeyboardHandler(this);
            browser.LifeSpanHandler = new BrowserLifeSpanHandler(this);
            browser.DisplayHandler = new BrowserDisplayHandler(this);
            browser.MenuHandler = new BrowserMenuHandler(this);
        }

        private static void OnFullScreenChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            BrowserWindow window = (BrowserWindow)d;
            if ((e.OldValue != e.NewValue) && (bool)e.NewValue)
            {
                window._isMaximizedBeforeFullScreen = window.WindowState == WindowState.Maximized;
                if (window._isMaximizedBeforeFullScreen)
                    window.WindowState = WindowState.Normal;

                window.Topmost = true;
                window.WindowStyle = WindowStyle.None;
                window.WindowState = WindowState.Maximized;
            }
            else
            {
                window.Topmost = false;
                window.WindowStyle = WindowStyle.SingleBorderWindow;
                window.WindowState = WindowState.Normal;

                if (window._isMaximizedBeforeFullScreen)
                    window.WindowState = WindowState.Maximized;
            }
        }

        private void WindowSourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd).AddHook(new HwndSourceHook(WndProc));
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateStatusPopupPosition();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x003: //WM_MOVE
                    {
                        UpdateStatusPopupPosition();
                        break;
                    }
            }
            return IntPtr.Zero;
        }

        private void UpdateStatusPopupPosition()
        {
            var pos = PointToScreen(new Point(0, mainGrid.ActualHeight - statusPopupContent.Height));
            statusPopup.PlacementRectangle = new Rect { X = pos.X, Y = pos.Y };
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
    }
}
