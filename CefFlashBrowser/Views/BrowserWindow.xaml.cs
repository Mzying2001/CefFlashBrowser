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

        public BrowserWindow(string url)
        {
            InitializeComponent();

            _viewModel = new BrowserWindowViewModel(url);
            DataContext = _viewModel;
            browserBase.Children.Add(_viewModel.Browser);
        }

        public static void Popup(string url)
        {
            new BrowserWindow(url).Show();
        }
    }
}
