using SimpleMvvm;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    public class JsDialogViewModelBase : ViewModelBase
    {
        private string _title;
        public string Title
        {
            get => _title;
            set => UpdateValue(ref _title, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => UpdateValue(ref _message, value);
        }
    }
}
