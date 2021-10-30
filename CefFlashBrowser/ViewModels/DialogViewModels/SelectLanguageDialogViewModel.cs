using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels.DialogViewModels
{
    public class SelectLanguageDialogViewModel : ViewModelBase
    {
        public DelegateCommand SelectLanguageCommand { get; set; }
        public DelegateCommand SetHeaderCommand { get; set; }

        private string _header;
        public string Header
        {
            get => _header;
            set => UpdateValue(ref _header, value);
        }

        private void SetHeader(string text)
        {
            Header = text;
        }

        private void SelectLanguage(string language)
        {
            LanguageManager.CurrentLanguage = language;
            Messenger.Global.Send(MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, GetType()), null);
        }

        public SelectLanguageDialogViewModel()
        {
            SelectLanguageCommand = new DelegateCommand<string>(SelectLanguage);
            SetHeaderCommand = new DelegateCommand<string>(SetHeader);
        }
    }
}
