using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using SimpleMvvm;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class ItemViewModel<TValue> : ViewModelBase, IDisposable
    {
        private bool _disposed = false;
        private string _nameLanguageKey = null;

        public TValue Value { get; set; }

        public string Name => GetName();

        public ItemViewModel()
        {
        }

        public ItemViewModel(TValue value)
        {
            Value = value;
        }

        public ItemViewModel(TValue value, string nameLanguageKey) : this(value)
        {
            _nameLanguageKey = nameLanguageKey ?? throw new ArgumentNullException(nameof(nameLanguageKey));
            Messenger.Global.Register(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
        }

        private void OnLanguageChanged(object _)
        {
            RaisePropertyChanged(nameof(Name));
        }

        private string GetName()
        {
            var languageService = SimpleIoc.Global.GetInstance<LanguageService>();
            return languageService?.GetString(_nameLanguageKey) ?? Value?.ToString();
        }

        ~ItemViewModel()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                }
                if (_nameLanguageKey != null)
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
