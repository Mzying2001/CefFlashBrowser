using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
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
        public ICommand SelectionChangedCommand { get; set; }
        public ICommand SaveChangesCommand { get; set; }
        public ICommand AddItemCommand { get; set; }
        public ICommand RemoveItemCommand { get; set; }
        public ICommand MoveUpCommand { get; set; }
        public ICommand MoveDownCommand { get; set; }
        public ICommand MoveToTopCommand { get; set; }
        public ICommand MoveToBottomCommand { get; set; }

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

            SelectedIndex = ((ListBox)sender).SelectedIndex;

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
        }

        private void SaveChanges()
        {
            if (Favorites.Items == null || Favorites.Items.Count == 0)
                return;

            try
            {
                var website = new Website(SelectedName.Trim(), SelectedUrl.Trim());
                Favorites.Items[SelectedIndex].Name = website.Name;
                Favorites.Items[SelectedIndex].Url = website.Url;
            }
            catch (Exception e)
            {
                JsAlertDialog.Show(e.Message);
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
            var dr = JsConfirmDialog.Show(string.Format(LanguageManager.GetString("message_removeItem"),
                                                        Favorites.Items[SelectedIndex].Name));
            if (dr)
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

        private void MoveToTop()
        {
            var item = Favorites.Items[SelectedIndex];
            Favorites.Items.RemoveAt(SelectedIndex);
            Favorites.Items.Insert(0, item);
            SelectedIndex = 0;
        }

        private void MoveToBottom()
        {
            var item = Favorites.Items[SelectedIndex];
            Favorites.Items.RemoveAt(SelectedIndex);
            Favorites.Items.Add(item);
            SelectedIndex = Favorites.Items.Count - 1;
        }

        private void Init()
        {
            HasItems = Favorites.Items.Count != 0;
            if (HasItems)
            {
                SelectedIndex = 0;
                SelectedName = Favorites.Items[SelectedIndex].Name;
                SelectedUrl = Favorites.Items[SelectedIndex].Url;
            }
        }

        public FavoritesManagerViewModel()
        {
            Init();

            SelectionChangedCommand = new DelegateCommand(SelectionChanged);
            SaveChangesCommand = new DelegateCommand(SaveChanges);
            AddItemCommand = new DelegateCommand(AddItem);
            RemoveItemCommand = new DelegateCommand(RemoveItem);
            MoveUpCommand = new DelegateCommand(MoveUp);
            MoveDownCommand = new DelegateCommand(MoveDown);
            MoveToTopCommand = new DelegateCommand(MoveToTop);
            MoveToBottomCommand = new DelegateCommand(MoveToBottom);
        }
    }
}
