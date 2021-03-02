using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;

namespace CefFlashBrowser.ViewModels
{
    class LanguageMenuItemViewModel : NotificationObject
    {
        public event Action LanguageSwitched;

        public DelegateCommand SwitchLanguageCommand { get; set; }

        public string Language { get; set; }

        private string _languageName;

        public string LanguageName
        {
            get => _languageName;
            set
            {
                _languageName = value;
                RaisePropertyChanged("LanguageName");
            }
        }

        private bool _isCurrentLanguage;

        public bool IsCurrentLanguage
        {
            get => _isCurrentLanguage;
            set
            {
                _isCurrentLanguage = value;
                RaisePropertyChanged("IsCurrentLanguage");
            }
        }

        private void SwitchLanguage()
        {
            if (LanguageManager.CurrentLanguage == Language)
                return;

            LanguageManager.CurrentLanguage = Language;
            LanguageSwitched?.Invoke();
        }

        public LanguageMenuItemViewModel(string language)
        {
            Language = language;
            LanguageName = LanguageManager.GetLanguageName(language);

            SwitchLanguageCommand = new DelegateCommand()
            {
                Execute = p => SwitchLanguage()
            };
        }
    }
}
