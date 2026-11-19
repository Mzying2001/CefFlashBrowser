using CefFlashBrowser.Infrastructure.Collections;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Ioc;
using System.Collections.ObjectModel;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class FavoritesManagerViewModel : ViewModelBase
    {
        private LanguageService LanguageService
        {
            get => SimpleIoc.Global.GetInstance<LanguageService>();
        }

        public DelegateCommand AddItemCommand { get; set; }
        public DelegateCommand RemoveItemCommand { get; set; }
        public DelegateCommand MoveUpCommand { get; set; }
        public DelegateCommand MoveDownCommand { get; set; }
        public DelegateCommand MoveToTopCommand { get; set; }
        public DelegateCommand MoveToBottomCommand { get; set; }
        public DelegateCommand ReorderCommand { get; }


        public ObservableCollection<Website> Favorites
        {
            get => SimpleIoc.Global.GetInstance<FavoritesService>().Favorites;
        }

        private Website _selectedWebsite;
        public Website SelectedWebsite
        {
            get => _selectedWebsite;
            set => UpdateValue(ref _selectedWebsite, value);
        }


        private void AddItem()
        {
            var vm = new AddFavoriteDialogViewModel
            {
                Name = LanguageService.GetString("favorites_defaultName"),
                Url = "about:blank",
            };

            if (WindowManager.ShowAddFavoriteDialog(vm))
            {
                var website = vm.Website.Clone();
                Favorites.Add(website);
                SelectedWebsite = website;
            }
        }

        private void RemoveItem(Website item)
        {
            int index = Favorites.IndexOf(item);
            if (index == -1) return;

            var msg = LanguageService.GetFormattedString("message_removeItem", item.Name);

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

        private bool TryGetReorderIndices(object value, out int sourceIndex, out int destinationIndex)
        {
            sourceIndex = -1;
            destinationIndex = -1;

            if (!(value is ListReorderRequest request)
                || !(request.Item is Website item)
                || !(request.TargetItem is Website target))
                return false;

            var favorites = Favorites;
            sourceIndex = favorites.IndexOf(item);
            int targetIndex = favorites.IndexOf(target);

            if (sourceIndex < 0 || targetIndex < 0)
                return false;

            destinationIndex = targetIndex + (request.InsertAfter ? 1 : 0);

            if (sourceIndex < destinationIndex)
                destinationIndex--;

            return destinationIndex != sourceIndex;
        }

        private bool CanExecuteReorder(object value)
        {
            return TryGetReorderIndices(value, out _, out _);
        }

        private void Reorder(ListReorderRequest request)
        {
            if (TryGetReorderIndices(request, out int sourceIndex, out int destinationIndex))
                Favorites.Move(sourceIndex, destinationIndex);
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
            ReorderCommand = new DelegateCommand<ListReorderRequest>(Reorder) { CanExecuteFunc = CanExecuteReorder };
        }
    }
}
