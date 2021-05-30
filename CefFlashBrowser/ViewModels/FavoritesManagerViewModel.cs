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
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels
{
    class FavoritesManagerViewModel : NotificationObject
    {
        private bool _switchingIndexFlag = false;

        public ICommand SelectionChangedCommand { get; set; }

        public ICommand UpdateNameCommand { get; set; }

        public ICommand UpdateUrlCommand { get; set; }

        public ICommand SaveChangesCommand { get; set; }

        public ICommand AddItemCommand { get; set; }

        public ICommand RemoveItemCommand { get; set; }

        public ICommand MoveUpCommand { get; set; }

        public ICommand MoveDownCommand { get; set; }

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

        private void SelectionChanged(object sender)
        {
            if (Favorites.Items == null)
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
                if (Favorites.Items.Count != 0)
                {
                    HasItems = true;
                    SelectedName = Favorites.Items[SelectedIndex].Name;
                    SelectedUrl = Favorites.Items[SelectedIndex].Url;
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
            if (Favorites.Items == null || Favorites.Items.Count == 0)
                return;

            try
            {
                var website = new Website(SelectedName.Trim(), SelectedUrl.Trim());
                Favorites.Items[SelectedIndex] = website;
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }

        private void AddItem()
        {
            var website = new Website(LanguageManager.GetString("favorites_defaultName"), "about:blank");

            if (Favorites.Items.Count == 0 || Favorites.Items[Favorites.Items.Count - 1] != website)
                Favorites.Items.Add(website);

            SelectedIndex = Favorites.Items.Count - 1;
        }

        private void RemoveItem()
        {
            var r = MessageBox.Show(string.Format(LanguageManager.GetString("message_removeItem"), Favorites.Items[SelectedIndex].Name),
                                    string.Empty, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
            {
                Favorites.Items.RemoveAt(SelectedIndex--);
                if (SelectedIndex == -1 && Favorites.Items.Count > 0)
                    SelectedIndex = 0;
            }
        }

        private void SwapItem(int i, int j)
        {
            var temp = new Website(Favorites.Items[i].Name, Favorites.Items[i].Url);
            Favorites.Items[i].Name = Favorites.Items[j].Name;
            Favorites.Items[i].Url = Favorites.Items[j].Url;
            Favorites.Items[j].Name = temp.Name;
            Favorites.Items[j].Url = temp.Url;
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
            if (index < Favorites.Items.Count - 1)
            {
                SwapItem(index, index + 1);
                SelectedIndex++;
            }
        }

        public FavoritesManagerViewModel()
        {
            SelectionChangedCommand = new DelegateCommand(SelectionChanged);

            UpdateNameCommand = new DelegateCommand(name => UpdateName(name?.ToString()));

            UpdateUrlCommand = new DelegateCommand(url => UpdateUrl(url?.ToString()));

            SaveChangesCommand = new DelegateCommand(SaveChanges);

            AddItemCommand = new DelegateCommand(AddItem);

            RemoveItemCommand = new DelegateCommand(RemoveItem);

            MoveUpCommand = new DelegateCommand(MoveUp);

            MoveDownCommand = new DelegateCommand(MoveDown);

            SelectionChanged(null);
        }
    }
}
