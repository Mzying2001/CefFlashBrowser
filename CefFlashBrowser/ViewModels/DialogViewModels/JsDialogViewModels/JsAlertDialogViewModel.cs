using CefFlashBrowser.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels
{
    class JsAlertDialogViewModel : JsDialogViewModel
    {
        private void Ok()
        {
            DialogResult = "OK";
            CloseWindow?.Invoke();
        }

        public JsAlertDialogViewModel()
        {
            OkCommand = new DelegateCommand(Ok);
        }
    }
}
