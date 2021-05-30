using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.MenuItemViewModels
{
    class LanguageMenuItemViewModel : MenuItemViewModel
    {
        public event Action LanguageSwitched;

        public string Language { get; private set; }

        protected override void MenuItemSelected()
        {
            if (LanguageManager.CurrentLanguage == Language)
                return;

            LanguageManager.CurrentLanguage = Language;
            LanguageSwitched?.Invoke();
        }

        public LanguageMenuItemViewModel(string language) : base()
        {
            Language = language;
            Header = LanguageManager.GetLanguageName(language);
        }
    }
}
