using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
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

            var x = Settings.BrowserWindowX;
            var y = Settings.BrowserWindowY;
            var w = Settings.BrowserWindowWidth;
            var h = Settings.BrowserWindowHeight;
            if (x != default &&
                y != default &&
                w != default &&
                h != default)
            {
                Left = x;
                Top = y;
                Width = w;
                Height = h;
            }
        }

        private void CloseWindow(object obj)
        {
            Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.BrowserWindowX = Left;
            Settings.BrowserWindowY = Top;
            Settings.BrowserWindowWidth = Width;
            Settings.BrowserWindowHeight = Height;
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
