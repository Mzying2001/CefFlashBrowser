using CefFlashBrowser.Models;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    public class JsPromptDialogViewModel : JsDialogViewModelBase
    {
        public DelegateCommand OkCommand { get; set; }
        public DelegateCommand CalcelCommand { get; set; }

        private string _promptText;
        public string PromptText
        {
            get => _promptText;
            set => UpdateValue(ref _promptText, value);
        }

        private void Ok()
        {
            Messenger.Global.Send(MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, GetType()), PromptText);
        }

        private void Calcel()
        {
            Messenger.Global.Send(MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, GetType()), null);
        }

        public JsPromptDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
            CalcelCommand = new DelegateCommand(Calcel);
        }
    }
}
