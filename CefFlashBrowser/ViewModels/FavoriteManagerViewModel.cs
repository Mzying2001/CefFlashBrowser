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
        private bool _switchingIndexFlag = false;

        public DelegateCommand SelectionChangedCommand { get; set; }

        public DelegateCommand UpdateNameCommand { get; set; }

        public DelegateCommand UpdateUrlCommand { get; set; }

        public DelegateCommand SaveChangesCommand { get; set; }

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
            if (FavoriteItems == null || FavoriteItems.Count == 0)
                return;

            _switchingIndexFlag = true;
            SelectedIndex = (sender as ListBox)?.SelectedIndex ?? 0;
            SelectedName = FavoriteItems[SelectedIndex].Website.Name;
            SelectedUrl = FavoriteItems[SelectedIndex].Website.Url;
            _switchingIndexFlag = false;
        }

        private void UpdateName(string name)
        {
            if (!_switchingIndexFlag)
                SelectedName = name;
        }

        private void UpdateUrl(string url)
        {
            if (!_switchingIndexFlag)
                SelectedUrl = url;
        }

        private void SaveChanges()
        {
            System.Windows.MessageBox.Show($"{SelectedName}\n{SelectedUrl}","debug");
        }

        public FavoriteManagerViewModel()
        {
            SelectionChangedCommand = new DelegateCommand(SelectionChanged);

            UpdateNameCommand = new DelegateCommand(p => UpdateName(p?.ToString()));

            UpdateUrlCommand = new DelegateCommand(p => UpdateUrl(p?.ToString()));

            SaveChangesCommand = new DelegateCommand(p => SaveChanges());
        }
    }
}
