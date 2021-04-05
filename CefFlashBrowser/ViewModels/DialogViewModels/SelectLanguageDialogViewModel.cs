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

        public DelegateCommand SetHeaderCommand { get; set; }

        public Action CloseWindow { get; set; }

        private string _header;

        public string Header
        {
            get => _header;
            set
            {
                _header = value;
                RaisePropertyChanged("Header");
            }
        }

        private void SelectLanguage(string language)
        {
            LanguageManager.CurrentLanguage = language;
            CloseWindow?.Invoke();
        }

        private void SetHeader(string text)
        {
            Header = text;
        }

        public SelectLanguageDialogViewModel()
        {
            SelectLanguageCommand = new DelegateCommand(p => SelectLanguage(p?.ToString()));

            SetHeaderCommand = new DelegateCommand(p => SetHeader(p?.ToString()));
        }
    }
}
