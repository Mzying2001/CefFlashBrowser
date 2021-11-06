using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views.Dialogs;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class FavoritesManagerViewModel : ViewModelBase
    {
        public DelegateCommand SelectionChangedCommand { get; set; }
        public DelegateCommand SaveChangesCommand { get; set; }
        public DelegateCommand AddItemCommand { get; set; }
        public DelegateCommand RemoveItemCommand { get; set; }
        public DelegateCommand MoveUpCommand { get; set; }
        public DelegateCommand MoveDownCommand { get; set; }
        public DelegateCommand MoveToTopCommand { get; set; }
        public DelegateCommand MoveToBottomCommand { get; set; }

        private int _selectedIndex;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set => UpdateValue(ref _selectedIndex, value);
        }

        private string _selectedName;
        public string SelectedName
        {
            get => _selectedName;
            set => UpdateValue(ref _selectedName, value);
        }

        private string _selectedUrl;
        public string SelectedUrl
        {
            get => _selectedUrl;
            set => UpdateValue(ref _selectedUrl, value);
        }

        private void SelectionChanged()
        {
            if (Favorites.Items == null)
                return;

            if (SelectedIndex == -1 || Favorites.Items.Count == 0)
            {
                SelectedName = string.Empty;
                SelectedUrl = string.Empty;
            }
            else
            {
                Website item = Favorites.Items[SelectedIndex];
                SelectedName = item.Name;
                SelectedUrl = item.Url;
            }
        }

        private void SaveChanges()
        {
            if (Favorites.Items == null || Favorites.Items.Count == 0)
                return;

            try
            {
                int index = SelectedIndex;
                var website = new Website(SelectedName.Trim(), SelectedUrl.Trim());
                Favorites.Items.RemoveAt(index);
                Favorites.Items.Insert(index, website);
                SelectedIndex = index;
            }
            catch (Exception e)
            {
                JsAlertDialog.Show(e.Message);
            }
        }

        private void AddItem()
        {
            AddFavoriteDialog.Show(LanguageManager.GetString("favorites_defaultName"), "about:blank", result =>
            {
                if (result)
                    SelectedIndex = Favorites.Items.Count - 1;
            });
        }

        private void RemoveItem(Website item)
        {
            JsConfirmDialog.Show(string.Format(LanguageManager.GetString("message_removeItem"), Favorites.Items[SelectedIndex].Name), "", result =>
            {
                if (result == true)
                {
                    int index = SelectedIndex;
                    Favorites.Items.Remove(item);
                    SelectedIndex = (--index == -1 && Favorites.Items.Count > 0) ? 0 : index;
                }
            });
        }

        private void SwapItem(int i, int j)
        {
            if (i > j)
            {
                int tmp = i;
                i = j;
                j = tmp;
            }
            Website item1 = Favorites.Items[i];
            Website item2 = Favorites.Items[j];
            Favorites.Items.Remove(item2);
            Favorites.Items.Remove(item1);
            Favorites.Items.Insert(i, item2);
            Favorites.Items.Insert(j, item1);
        }

        private void MoveUp(Website item)
        {
            int index = Favorites.Items.IndexOf(item);
            if (index > 0)
            {
                SwapItem(index, index - 1);
                SelectedIndex = index - 1;
            }
        }

        private void MoveDown(Website item)
        {
            int index = Favorites.Items.IndexOf(item);
            if (index < Favorites.Items.Count - 1)
            {
                SwapItem(index, index + 1);
                SelectedIndex = index + 1;
            }
        }

        private void MoveToTop(Website item)
        {
            Favorites.Items.Remove(item);
            Favorites.Items.Insert(0, item);
            SelectedIndex = 0;
        }

        private void MoveToBottom(Website item)
        {
            Favorites.Items.Remove(item);
            Favorites.Items.Add(item);
            SelectedIndex = Favorites.Items.Count - 1;
        }

        protected override void Init()
        {
            base.Init();

            if (Favorites.Items.Count != 0)
            {
                SelectedIndex = 0;
                SelectedName = Favorites.Items[SelectedIndex].Name;
                SelectedUrl = Favorites.Items[SelectedIndex].Url;
            }
        }

        public FavoritesManagerViewModel()
        {
            SelectionChangedCommand = new DelegateCommand(SelectionChanged);
            SaveChangesCommand = new DelegateCommand(SaveChanges);
            AddItemCommand = new DelegateCommand(AddItem);
            RemoveItemCommand = new DelegateCommand<Website>(RemoveItem);
            MoveUpCommand = new DelegateCommand<Website>(MoveUp);
            MoveDownCommand = new DelegateCommand<Website>(MoveDown);
            MoveToTopCommand = new DelegateCommand<Website>(MoveToTop);
            MoveToBottomCommand = new DelegateCommand<Website>(MoveToBottom);
        }
    }
}
