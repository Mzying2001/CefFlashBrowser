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

            Messenger.Global.Register(MessageTokens.EXIT_BROWSER, ExitBrowser);

            if (GlobalData.Settings.BrowserWindowSizeInfo != null)
                GlobalData.Settings.BrowserWindowSizeInfo.Apply(this);

            browser.OnClose += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    _doClose = true;
                    Close();
                });
            };
        }

        private void ExitBrowser(object browser)
        {
            if (ReferenceEquals(browser, this.browser)) Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_doClose)
            {
                browserWindow.WindowState = WindowState.Normal;
                GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
                Messenger.Global.Unregister(MessageTokens.EXIT_BROWSER, ExitBrowser);
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
