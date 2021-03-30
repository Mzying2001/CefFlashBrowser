using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
            (DataContext as BrowserWindowViewModel).Browser = browser;

            var w = Settings.BrowserWindowWidth;
            var h = Settings.BrowserWindowHeight;
            if (w != default && h != default)
            {
                Width = w;
                Height = h;
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.BrowserWindowWidth = Width;
            Settings.BrowserWindowHeight = Height;
        }

        private void OtherButton_Clicked(object sender, RoutedEventArgs e)
        {
            otherButtonContextMenu.PlacementTarget = sender as UIElement;
            otherButtonContextMenu.IsOpen = true;
        }

        public static void Popup(string url, bool showNavigationBar = true)
        {
            var window = new BrowserWindow();
            var vModel = window.DataContext as BrowserWindowViewModel;

            if (vModel == null)
            {
                window.Close();
                return;
            }

            if (!showNavigationBar)
            {
                window.browser.OnCreatedNewWindow += (s, e) =>
                {
                    e.CancelPopup = false;
                };
            }

            vModel.LoadUrlCommand.Execute(url);
            vModel.ShowNavigationBar = showNavigationBar;
            window.Show();
        }

        public static void PopupFlashPlayer(string fileName)
        {
            Popup(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"html/FlashPlayer.html?src={fileName}"), false);
        }
    }
}
