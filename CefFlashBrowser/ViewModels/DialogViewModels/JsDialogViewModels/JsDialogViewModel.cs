using SimpleMvvm;
using System;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsDialogViewModel<ResultType> : ViewModelBase
    {
        public Action CloseWindow { get; set; }

        public ResultType DialogResult { get; protected set; }

        private string _title;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                RaisePropertyChanged("Title");
            }
        }

        private string _message;

        public string Message
        {
            get => _message;
            set
            {
                _message = value;
                RaisePropertyChanged("Message");
            }
        }
    }
}
