using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels.DialogViewModels
{
    class AddFavoriteDialogViewModel : NotificationObject
    {
        public ICommand OkCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public Action CloseWindow { get; set; }

        public bool Result { get; private set; }

        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged("Name");
            }
        }

        private string _url;

        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                RaisePropertyChanged("Url");
            }
        }

        private void Ok()
        {
            try
            {
                var website = new Website(Name, Url);
                Favorites.Add(website);
                Result = true;
                CloseWindow?.Invoke();
            }
            catch (Exception e)
            {
                JsAlertDialog.Show(e.Message, LanguageManager.GetString("title_error"));
            }
        }

        private void Cancel()
        {
            Result = false;
            CloseWindow?.Invoke();
        }

        public AddFavoriteDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
            CancelCommand = new DelegateCommand(Cancel);
        }
    }
}
