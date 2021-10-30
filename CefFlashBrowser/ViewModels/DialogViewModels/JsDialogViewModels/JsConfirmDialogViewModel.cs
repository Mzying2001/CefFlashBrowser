using SimpleMvvm.Command;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsConfirmDialogViewModel : JsDialogViewModel<bool>
    {
        public DelegateCommand YesCommand { get; set; }
        public DelegateCommand NoCommand { get; set; }

        private void Yes()
        {
            DialogResult = true;
            CloseWindow?.Invoke();
        }

        private void No()
        {
            DialogResult = false;
            CloseWindow?.Invoke();
        }

        public JsConfirmDialogViewModel()
        {
            YesCommand = new DelegateCommand(Yes);
            NoCommand = new DelegateCommand(No);
        }
    }
}
