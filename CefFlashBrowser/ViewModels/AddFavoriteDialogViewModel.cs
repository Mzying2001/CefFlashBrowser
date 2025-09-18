using CefFlashBrowser.Models.Data;
using SimpleMvvm;
using SimpleMvvm.Command;

namespace CefFlashBrowser.ViewModels
{
    public class AddFavoriteDialogViewModel : ViewModelBase
    {
        public DelegateCommand AddFavoriteCommand { get; set; }
        public DelegateCommand CancelAddCommand { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (UpdateValue(ref _name, value))
                    AddFavoriteCommand.RaiseCanExecuteChanged();
            }
        }

        private string _url = string.Empty;
        public string Url
        {
            get => _url;
            set
            {
                if (UpdateValue(ref _url, value))
                    AddFavoriteCommand.RaiseCanExecuteChanged();
            }
        }

        private bool? _dialogResult = null;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => UpdateValue(ref _dialogResult, value);
        }

        private void AddFavorite()
        {
            GlobalData.Favorites.Add(new Models.Website(Name, Url));
            DialogResult = true;
        }

        private bool CanAddFavorite(object _)
        {
            return !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(Url);
        }

        private void CancelAdd()
        {
            DialogResult = false;
        }

        public AddFavoriteDialogViewModel()
        {
            AddFavoriteCommand = new DelegateCommand(AddFavorite) { CanExecuteFunc = CanAddFavorite };
            CancelAddCommand = new DelegateCommand(CancelAdd);
        }
    }
}
