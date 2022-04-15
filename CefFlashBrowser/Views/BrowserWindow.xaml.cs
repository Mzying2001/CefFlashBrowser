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

            if (GlobalData.Settings.BrowserWindowSizeInfo != null)
                GlobalData.Settings.BrowserWindowSizeInfo.Apply(this);

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
            Point leftBottom = PointToScreen(new Point(0, mainGrid.ActualHeight - statusPopupContent.Height));
            statusPopup.PlacementRectangle = new Rect
            {
                X = leftBottom.X,
                Y = leftBottom.Y
            };
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

        private void BrowserOnClose(object sender, System.EventArgs e)
        {
            _doClose = true;
            Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_doClose)
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

        private void MenuButtonClicked(object sender, RoutedEventArgs e)
        {
            var contextMenu = (ContextMenu)Resources["more"];
            contextMenu.PlacementTarget = sender as UIElement;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        private void ShowBlockedSwfsButtonClicked(object sender, RoutedEventArgs e)
        {
            var contextMenu = (ContextMenu)Resources["blockedSwfs"];
            contextMenu.PlacementTarget = sender as UIElement;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        public static void Show(string address)
        {
            var window = new BrowserWindow();
            window.browser.Load(address);
            window.Show();
        }
    }
}
