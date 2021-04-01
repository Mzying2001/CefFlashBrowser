using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.ViewModels
{
    class FavoritesManagerViewModel : NotificationObject
    {
        private bool _switchingIndexFlag = false;

        public DelegateCommand SelectionChangedCommand { get; set; }

        public DelegateCommand UpdateNameCommand { get; set; }

        public DelegateCommand UpdateUrlCommand { get; set; }

        public DelegateCommand SaveChangesCommand { get; set; }

        public DelegateCommand AddItemCommand { get; set; }

        public DelegateCommand RemoveItemCommand { get; set; }

        public DelegateCommand MoveUpCommand { get; set; }

        public DelegateCommand MoveDownCommand { get; set; }

        public ObservableCollection<FavoritesMenuItemVliewModel> FavoritesItems { get; set; }

        private bool _hasItems;

        public bool HasItems
        {
            get => _hasItems;
            set
            {
                _hasItems = value;
                RaisePropertyChanged("HasItems");
            }
        }

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

        public void SetFavoritesItems(ObservableCollection<FavoritesMenuItemVliewModel> favoritesItems)
        {
            FavoritesItems = favoritesItems;
            RaisePropertyChanged("FavoritesItems");
            SelectionChanged(null);
        }

        private void SelectionChanged(object sender)
        {
            if (FavoritesItems == null)
                return;

            _switchingIndexFlag = true;
            SelectedIndex = (sender as ListBox)?.SelectedIndex ?? 0;
            if (SelectedIndex == -1)
            {
                HasItems = false;
                SelectedName = string.Empty;
                SelectedUrl = string.Empty;
            }
            else
            {
                if (FavoritesItems.Count != 0)
                {
                    HasItems = true;
                    SelectedName = FavoritesItems[SelectedIndex].Website.Name;
                    SelectedUrl = FavoritesItems[SelectedIndex].Website.Url;
                }
            }
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
            if (FavoritesItems == null || FavoritesItems.Count == 0)
                return;

            try
            {
                var website = new Website(SelectedName.Trim(), SelectedUrl.Trim());
                FavoritesItems[SelectedIndex].Website = website;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void AddItem()
        {
            var website = new Website(LanguageManager.GetString("favorites_defaultName"), "about:blank");

            if (FavoritesItems.Count == 0 || FavoritesItems[FavoritesItems.Count - 1].Website != website)
                FavoritesItems.Add(new FavoritesMenuItemVliewModel(website));

            SelectedIndex = FavoritesItems.Count - 1;
        }

        private void RemoveItem()
        {
            var r = MessageBox.Show(string.Format(LanguageManager.GetString("message_removeItem"), FavoritesItems[SelectedIndex].Header),
                                    string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                FavoritesItems.RemoveAt(SelectedIndex--);
                if (SelectedIndex == -1 && FavoritesItems.Count > 0)
                    SelectedIndex = 0;
            }
        }

        private void SwapItem(int i, int j)
        {
            var temp = FavoritesItems[i].Website;
            FavoritesItems[i].Website = FavoritesItems[j].Website;
            FavoritesItems[j].Website = temp;
        }

        private void MoveUp()
        {
            int index = SelectedIndex;
            if (index > 0)
            {
                SwapItem(index, index - 1);
                SelectedIndex--;
            }
        }

        private void MoveDown()
        {
            int index = SelectedIndex;
            if (index < FavoritesItems.Count - 1)
            {
                SwapItem(index, index + 1);
                SelectedIndex++;
            }
        }

        public FavoritesManagerViewModel()
        {
            SelectionChangedCommand = new DelegateCommand(SelectionChanged);

            UpdateNameCommand = new DelegateCommand(p => UpdateName(p?.ToString()));

            UpdateUrlCommand = new DelegateCommand(p => UpdateUrl(p?.ToString()));

            SaveChangesCommand = new DelegateCommand(p => SaveChanges());

            AddItemCommand = new DelegateCommand(p => AddItem());

            RemoveItemCommand = new DelegateCommand(p => RemoveItem());

            MoveUpCommand = new DelegateCommand(p => MoveUp());

            MoveDownCommand = new DelegateCommand(p => MoveDown());
        }
    }
}
