using CefFlashBrowser.Models;
using SimpleMvvm;
using SimpleMvvm.Command;

namespace CefFlashBrowser.ViewModels
{
    public class AddFavoriteDialogViewModel : ViewModelBase
    {
        public DelegateCommand ConfirmCommand { get; set; }
        public DelegateCommand CancelCommand { get; set; }


        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (UpdateValue(ref _name, value))
                    ConfirmCommand.RaiseCanExecuteChanged();
            }
        }

        private string _url = string.Empty;
        public string Url
        {
            get => _url;
            set
            {
                if (UpdateValue(ref _url, value))
                    ConfirmCommand.RaiseCanExecuteChanged();
            }
        }

        private bool? _dialogResult = null;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => UpdateValue(ref _dialogResult, value);
        }

        public Website Website
        {
            get => new Website(Name, Url);
        }


        private void Confirm()
        {
            DialogResult = true;
        }

        private bool CanConfirm(object _)
        {
            return !string.IsNullOrWhiteSpace(Name)
                && !string.IsNullOrWhiteSpace(Url);
        }

        private void Cancel()
        {
            DialogResult = false;
        }

        public AddFavoriteDialogViewModel()
        {
            ConfirmCommand = new DelegateCommand(Confirm) { CanExecuteFunc = CanConfirm };
            CancelCommand = new DelegateCommand(Cancel);
        }
    }
}
