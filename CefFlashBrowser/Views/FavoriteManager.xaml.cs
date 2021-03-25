using CefFlashBrowser.ViewModels;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// FavoriteManager.xaml 的交互逻辑
    /// </summary>
    public partial class FavoriteManager : Window
    {
        private FavoriteManagerViewModel _viewModel;

        public FavoriteManager()
        {
            InitializeComponent();
            _viewModel = DataContext as FavoriteManagerViewModel;
        }

        internal FavoriteManager(ObservableCollection<FavoriteMenuItemVliewModel> favorites) : this()
        {
            _viewModel.FavoriteItems = favorites;
            _viewModel.RaisePropertyChanged("FavoriteItems");
            _viewModel.SelectionChangedCommand.Execute.Invoke(null);
        }
    }
}
