using CefFlashBrowser.Commands;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CefFlashBrowser.ViewModels
{
    class FavoriteManagerViewModel : NotificationObject
    {
        public DelegateCommand SelectionChangedCommand { get; set; }

        public ObservableCollection<FavoriteMenuItemVliewModel> FavoriteItems { get; set; }

        private int _selectedIndex;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                _selectedIndex = value;
                RaisePropertyChanged("SelectedIndex");
            }
        }

        private string _selectedName;

        public string SelectedName
        {
            get => _selectedName;
            set
            {
                _selectedName = value;
                RaisePropertyChanged("SelectedName");
            }
        }

        private string _selectedUrl;

        public string SelectedUrl
        {
            get => _selectedUrl;
            set
            {
                _selectedUrl = value;
                RaisePropertyChanged("SelectedUrl");
            }
        }

        private void SelectionChanged(object sender)
        {
            if (FavoriteItems.Count == 0)
                return;

            SelectedIndex = (sender as ListBox)?.SelectedIndex ?? 0;
            SelectedName = FavoriteItems[SelectedIndex].Website.Name;
            SelectedUrl = FavoriteItems[SelectedIndex].Website.Url;
        }

        public FavoriteManagerViewModel()
        {
            SelectionChangedCommand = new DelegateCommand(SelectionChanged);
        }
    }
}
