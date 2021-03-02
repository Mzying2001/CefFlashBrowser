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
using System.Windows.Navigation;
using System.Windows.Shapes;
using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels;
using CefSharp;
using CefSharp.Wpf;

namespace CefFlashBrowser
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            _viewModel = new MainWindowViewModel();
            DataContext = _viewModel;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _viewModel.Url = (sender as TextBox).Text;
        }
    }
}
