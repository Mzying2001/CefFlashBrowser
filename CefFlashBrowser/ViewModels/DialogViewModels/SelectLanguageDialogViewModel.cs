using CefFlashBrowser.Commands;
using CefFlashBrowser.Models.StaticData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.DialogViewModels
{
    class SelectLanguageDialogViewModel : NotificationObject
    {
        public DelegateCommand SelectLanguageCommand { get; set; }

        private void SelectLanguage(string language)
        {
            LanguageManager.CurrentLanguage = language;
        }

        public SelectLanguageDialogViewModel()
        {
            SelectLanguageCommand = new DelegateCommand(p => SelectLanguage(p?.ToString()));
        }
    }
}
