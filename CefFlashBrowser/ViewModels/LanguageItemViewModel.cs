using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels
{
    public class LanguageItemViewModel : ViewModelBase
    {
        public string Language { get; }

        public string LanguageName
        {
            get => LanguageManager.GetLanguageName(Language) ?? Language;
        }

        public bool IsCurrentLanguage
        {
            get => Language == LanguageManager.CurrentLanguage;
        }

        private void OnLanguageChanged(object _)
        {
            RaisePropertyChanged(nameof(IsCurrentLanguage));
        }

        public LanguageItemViewModel()
        {
        }

        public LanguageItemViewModel(string language)
        {
            if (language != null)
            {
                Language = language;
                Messenger.Global.Register(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
            }
        }

        ~LanguageItemViewModel()
        {
            if (Language != null)
                Messenger.Global.Unregister(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
        }
    }
}
