using CefFlashBrowser.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsPromptDialogViewModel : JsDialogViewModel<(bool, string)>
    {
        public ICommand OkCommand { get; set; }
        public ICommand CalcelCommand { get; set; }

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
