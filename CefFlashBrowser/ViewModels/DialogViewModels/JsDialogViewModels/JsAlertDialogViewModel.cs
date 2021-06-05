using CefFlashBrowser.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsAlertDialogViewModel : JsDialogViewModel<bool>
    {
        public ICommand OkCommand { get; set; }

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
