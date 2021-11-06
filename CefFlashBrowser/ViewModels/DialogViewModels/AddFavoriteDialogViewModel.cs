using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels.DialogViewModels
{
    public class AddFavoriteDialogViewModel : ViewModelBase
    {
        public DelegateCommand OkCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set => UpdateValue(ref _name, value);
        }

        private string _url;
        public string Url
        {
            get => _url;
            set => UpdateValue(ref _url, value);
        }

        private void Ok()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Url))
                return;

            var website = new Website(Name.Trim(), Url.Trim());
            GlobalData.Favorites.Add(website);
            Messenger.Global.Send(MessageTokens.EXIT_ADDFAVORITES, true);
        }

        private void Cancel()
        {
            Messenger.Global.Send(MessageTokens.EXIT_ADDFAVORITES, false);
        }

        public AddFavoriteDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
            CancelCommand = new DelegateCommand(Cancel);
        }
    }
}
