using CefFlashBrowser.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsConfirmDialogViewModel : JsDialogViewModel<bool>
    {
        public ICommand YesCommand { get; set; }
        public ICommand NoCommand { get; set; }

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
