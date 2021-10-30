using SimpleMvvm.Command;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsAlertDialogViewModel : JsDialogViewModel<bool>
    {
        public DelegateCommand OkCommand { get; set; }

        private void Ok()
        {
            DialogResult = true;
            CloseWindow?.Invoke();
        }

        public JsAlertDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
        }
    }
}
