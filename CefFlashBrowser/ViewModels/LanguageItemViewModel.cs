using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using SimpleMvvm;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class LanguageItemViewModel : ViewModelBase, IDisposable
    {
        private LanguageService LanguageService
        {
            get => SimpleIoc.Global.GetInstance<LanguageService>();
        }

        private bool _disposed;

        public string Language { get; }

        public string LanguageName
        {
            get => LanguageService.GetLanguageName(Language) ?? Language;
        }

        public bool IsCurrentLanguage
        {
            get => Language == LanguageService.GetCurrentLanguage();
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
            Dispose(disposing: false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }
                if (Language != null)
                {
                    Messenger.Global.Unregister(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
