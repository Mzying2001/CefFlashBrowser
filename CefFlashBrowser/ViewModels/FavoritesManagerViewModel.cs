using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;

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
            if (GlobalData.Favorites == null)
                return;

            if (SelectedIndex == -1 || GlobalData.Favorites.Count == 0)
            {
                SelectedName = string.Empty;
                SelectedUrl = string.Empty;
            }
            else
            {
                Website item = GlobalData.Favorites[SelectedIndex];
                SelectedName = item.Name;
                SelectedUrl = item.Url;
            }
        }

        private void SaveChanges()
        {
            if (string.IsNullOrWhiteSpace(SelectedName) || string.IsNullOrWhiteSpace(SelectedUrl))
                return;

            int index = SelectedIndex;
            var website = new Website(SelectedName.Trim(), SelectedUrl.Trim());
            GlobalData.Favorites.RemoveAt(index);
            GlobalData.Favorites.Insert(index, website);
            SelectedIndex = index;
        }

        private void AddItem()
        {
            if (WindowManager.ShowAddFavoriteDialog(LanguageManager.GetString("favorites_defaultName"), "about:blank"))
            {
                SelectedIndex = GlobalData.Favorites.Count - 1;
            }
        }

        private void RemoveItem(Website item)
        {
            var msg = LanguageManager.GetFormattedString("message_removeItem", item.Name);

            WindowManager.Confirm(msg, callback: result =>
            {
                if (result == true)
                {
                    int index = SelectedIndex;
                    GlobalData.Favorites.Remove(item);
                    SelectedIndex = (--index == -1 && GlobalData.Favorites.Count > 0) ? 0 : index;
                }
            });
        }

        private void MoveUp(Website item)
        {
            int index = GlobalData.Favorites.IndexOf(item);

            if (index > 0)
            {
                GlobalData.Favorites.Move(index, index - 1);
            }
        }

        private void MoveDown(Website item)
        {
            int index = GlobalData.Favorites.IndexOf(item);

            if (index < GlobalData.Favorites.Count - 1)
            {
                GlobalData.Favorites.Move(index, index + 1);
            }
        }

        private void MoveToTop(Website item)
        {
            int index = GlobalData.Favorites.IndexOf(item);

            if (index > 0)
            {
                GlobalData.Favorites.Move(index, 0);
            }
        }

        private void MoveToBottom(Website item)
        {
            int index = GlobalData.Favorites.IndexOf(item);

            if (index < GlobalData.Favorites.Count - 1)
            {
                GlobalData.Favorites.Move(index, GlobalData.Favorites.Count - 1);
            }
        }

        protected override void Init()
        {
            base.Init();

            if (GlobalData.Favorites.Count != 0)
            {
                SelectedIndex = 0;
                SelectedName = GlobalData.Favorites[SelectedIndex].Name;
                SelectedUrl = GlobalData.Favorites[SelectedIndex].Url;
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
