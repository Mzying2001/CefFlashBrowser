using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
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
        private bool _doClose = false;

        public BrowserWindow()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GlobalData.Settings.BrowserWindowSizeInfo, this);

            browser.KeyboardHandler = new BrowserKeyboardHandler(browser);
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

        private void OnCreateNewBrowser(object sender, LifeSpanHandler.NewBrowserEventArgs e)
        {
            e.Handled = true;

            if (e.OpenDisposition == CefSharp.WindowOpenDisposition.NewPopup)
            {
                PopupWebPage.Show(e.TargetUrl, e.PopupFeatures);
            }
            else
            {
                switch (GlobalData.Settings.NewPageBehavior)
                {
                    case NewPageBehavior.NewWindow:
                        {
                            Show(e.TargetUrl);
                            break;
                        }
                    case NewPageBehavior.OriginalWindow:
                        {
                            browser.Load(e.TargetUrl);
                            break;
                        }
                }
            }
        }

        private void BrowserOnClose(object sender, EventArgs e)
        {
            _doClose = true;
            Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (browser.IsDisposed || _doClose)
            {
                browserWindow.WindowState = WindowState.Normal;
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

        public static void Show(string address)
        {
            var window = new BrowserWindow();
            window.browser.Load(address);
            window.Show();
        }
    }
}
