using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.SettingPanelViewModels;
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

namespace CefFlashBrowser.Views.SettingPanels
{
    /// <summary>
    /// MainSettingPanel.xaml 的交互逻辑
    /// </summary>
    public partial class MainSettingPanel : UserControl
    {
        private MainSettingPanelViewModel _viewModel;

        public MainSettingPanel()
        {
            InitializeComponent();

            _viewModel = new MainSettingPanelViewModel();
            DataContext = _viewModel;

            SearchEnginesComboBox.SelectedIndex = (int)Settings.SearchEngine;
        }

        private void SearchEngines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _viewModel.SwitchSearchEngine((SearchEngine.Engine)SearchEnginesComboBox.SelectedValue);
        }
    }
}
