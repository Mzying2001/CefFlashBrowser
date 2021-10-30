using SimpleMvvm.Command;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsPromptDialogViewModel : JsDialogViewModel<(bool, string)>
    {
        public DelegateCommand OkCommand { get; set; }
        public DelegateCommand CalcelCommand { get; set; }

        private string _promptText;

        public string PromptText
        {
            get => _promptText;
            set
            {
                _promptText = value;
                RaisePropertyChanged("PromptText");
            }
        }

        private void Ok()
        {
            DialogResult = (true, PromptText);
            CloseWindow?.Invoke();
        }

        private void Calcel()
        {
            DialogResult = (false, null);
            CloseWindow?.Invoke();
        }

        public JsPromptDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
            CalcelCommand = new DelegateCommand(Calcel);
        }
    }
}
