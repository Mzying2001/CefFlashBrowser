using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;

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
            try
            {
                var website = new Website(Name, Url);
                Favorites.Add(website);
                Messenger.Global.Send(MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, GetType()), true);
            }
            catch (Exception e)
            {
                JsAlertDialog.Show(e.Message, LanguageManager.GetString("title_error"));
            }
        }

        private void Cancel()
        {
            Messenger.Global.Send(MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, GetType()), false);
        }

        public AddFavoriteDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
            CancelCommand = new DelegateCommand(Cancel);
        }
    }
}
