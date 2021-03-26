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
    public partial class FavoritesManager : Window
    {
        private FavoritesManagerViewModel _viewModel;

        public FavoritesManager()
        {
            InitializeComponent();
            _viewModel = DataContext as FavoritesManagerViewModel;
        }

        internal FavoritesManager(ObservableCollection<FavoritesMenuItemVliewModel> favorites) : this()
        {
            _viewModel.FavoritesItems = favorites;
            _viewModel.RaisePropertyChanged("FavoritesItems");
            _viewModel.SelectionChangedCommand.Execute.Invoke(null);
        }
    }
}
