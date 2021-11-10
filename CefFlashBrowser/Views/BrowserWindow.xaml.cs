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
        public BrowserWindow()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.EXIT_BROWSER, CloseWindow);

            if (GlobalData.Settings.BrowserWindowSizeInfo != null)
                GlobalData.Settings.BrowserWindowSizeInfo.Apply(this);
        }

        private void CloseWindow(object obj)
        {
            Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            browserWindow.WindowState = WindowState.Normal;
            GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);

            browser.Dispose();

            Messenger.Global.Unregister(MessageTokens.EXIT_BROWSER, CloseWindow);
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
