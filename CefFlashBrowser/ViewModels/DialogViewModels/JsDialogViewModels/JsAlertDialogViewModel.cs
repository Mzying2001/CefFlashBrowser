using CefFlashBrowser.Models;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    public class JsAlertDialogViewModel : JsDialogViewModelBase
    {
        public DelegateCommand OkCommand { get; set; }

        private void Ok()
        {
            Messenger.Global.Send(MessageTokens.EXIT_JSALERT, null);
        }

        public JsAlertDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
        }
    }
}
