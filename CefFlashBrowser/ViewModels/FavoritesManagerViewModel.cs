using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System.Collections.ObjectModel;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class FavoritesManagerViewModel : ViewModelBase
    {
        public DelegateCommand AddItemCommand { get; set; }
        public DelegateCommand RemoveItemCommand { get; set; }
        public DelegateCommand MoveUpCommand { get; set; }
        public DelegateCommand MoveDownCommand { get; set; }
        public DelegateCommand MoveToTopCommand { get; set; }
        public DelegateCommand MoveToBottomCommand { get; set; }


        public ObservableCollection<Website> Favorites
        {
            get => GlobalData.Favorites;
        }

        private Website _selectedWebsite;
        public Website SelectedWebsite
        {
            get => _selectedWebsite;
            set => UpdateValue(ref _selectedWebsite, value);
        }


        private void AddItem()
        {
            if (WindowManager.ShowAddFavoriteDialog(
                LanguageManager.GetString("favorites_defaultName"), "about:blank"))
            {
                SelectedWebsite = Favorites.LastOrDefault();
            }
        }

        private void RemoveItem(Website item)
        {
            int index = Favorites.IndexOf(item);
            if (index == -1) return;

            var msg = LanguageManager.GetFormattedString("message_removeItem", item.Name);

            WindowManager.Confirm(msg, callback: result =>
            {
                if (result == true)
                {
                    Favorites.Remove(item);

                    if (index == 0)
                    {
                        SelectedWebsite = Favorites.FirstOrDefault();
                    }
                    else
                    {
                        SelectedWebsite = Favorites[index - 1];
                    }
                }
            });
        }

        private void MoveUp(Website item)
        {
            int index = Favorites.IndexOf(item);

            if (index > 0)
            {
                Favorites.Move(index, index - 1);
            }
        }

        private void MoveDown(Website item)
        {
            int index = Favorites.IndexOf(item);

            if (index < Favorites.Count - 1)
            {
                Favorites.Move(index, index + 1);
            }
        }

        private void MoveToTop(Website item)
        {
            int index = Favorites.IndexOf(item);

            if (index > 0)
            {
                Favorites.Move(index, 0);
            }
        }

        private void MoveToBottom(Website item)
        {
            int index = Favorites.IndexOf(item);

            if (index < Favorites.Count - 1)
            {
                Favorites.Move(index, Favorites.Count - 1);
            }
        }

        protected override void Init()
        {
            base.Init();
            SelectedWebsite = Favorites?.FirstOrDefault();
        }

        public FavoritesManagerViewModel()
        {
            AddItemCommand = new DelegateCommand(AddItem);
            RemoveItemCommand = new DelegateCommand<Website>(RemoveItem);
            MoveUpCommand = new DelegateCommand<Website>(MoveUp);
            MoveDownCommand = new DelegateCommand<Website>(MoveDown);
            MoveToTopCommand = new DelegateCommand<Website>(MoveToTop);
            MoveToBottomCommand = new DelegateCommand<Website>(MoveToBottom);
        }
    }
}
