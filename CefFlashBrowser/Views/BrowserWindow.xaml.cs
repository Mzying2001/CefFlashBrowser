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
        private BrowserWindowViewModel _viewModel;

        public BrowserWindow()
        {
            InitializeComponent();

            _viewModel = new BrowserWindowViewModel();
            DataContext = _viewModel;
        }

        public static void Popup(string url)
        {
            var window = new BrowserWindow();
            var vModel = window.DataContext as BrowserWindowViewModel;
            vModel.Url = url;
            window.Show();
        }

        public static void PopupFlashPlayer(string fileName)
        {
            Popup(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"html/FlashPlayer.html?src={fileName}"));
        }

        private void FlashBrowser_TitleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            _viewModel.Title = e.NewValue.ToString();
        }
    }
}
