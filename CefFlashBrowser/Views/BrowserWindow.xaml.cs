using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
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

            Messenger.Global.Register(MessageTokens.CLOSE_BROWSER, CloseBrowser);

            if (GlobalData.Settings.BrowserWindowSizeInfo != null)
                GlobalData.Settings.BrowserWindowSizeInfo.Apply(this);

            browser.MenuHandler = new BrowserWindowMenuHandler();
        }

        private void CloseBrowser(object browser)
        {
            if (ReferenceEquals(browser, this.browser)) Close();
        }

        private void Browser_OnCreateNewWindow(object sender, LifeSpanHandler.NewWindowEventArgs e)
        {
            e.CancelPopup = true;
            Dispatcher.Invoke(() => browser.Load(e.TargetUrl));
        }

        private void Browser_OnClose(object sender, System.EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _doClose = true;
                Close();
            });
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_doClose)
            {
                browserWindow.WindowState = WindowState.Normal;
                GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
                Messenger.Global.Unregister(MessageTokens.CLOSE_BROWSER, CloseBrowser);
            }
            else
            {
                browser.GetBrowser().CloseBrowser(false);
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
            window.browser.Address = address;
            window.Show();
        }
    }
}
