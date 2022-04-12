using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

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

        private void Browser_OnCreateNewWindow(object sender, LifeSpanHandler.NewWindowEventArgs e)
        {
            e.CancelPopup = true;
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

        private void Browser_OnClose(object sender, System.EventArgs e)
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

        private void MenuButton_Clicked(object sender, RoutedEventArgs e)
        {
            var contextMenu = (ContextMenu)Resources["more"];
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
