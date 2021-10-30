using CefFlashBrowser.Models;
using System;

namespace CefFlashBrowser.ViewModels.MenuItemViewModels
{
    public class LanguageMenuItemViewModel : MenuItemViewModel
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
