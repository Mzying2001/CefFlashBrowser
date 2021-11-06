using CefFlashBrowser.Models.Data;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    public class JsConfirmDialogViewModel : JsDialogViewModelBase
    {
        public DelegateCommand YesCommand { get; set; }
        public DelegateCommand NoCommand { get; set; }

        private void Yes()
        {
            Messenger.Global.Send(MessageTokens.EXIT_JSCONFIRM, true);
        }

        private void No()
        {
            Messenger.Global.Send(MessageTokens.EXIT_JSCONFIRM, false);
        }

        public JsConfirmDialogViewModel()
        {
            YesCommand = new DelegateCommand(Yes);
            NoCommand = new DelegateCommand(No);
        }
    }
}
