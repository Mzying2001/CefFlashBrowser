using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class ItemViewModel<TValue> : ViewModelBase, IDisposable
    {
        private bool _disposed = false;

        public TValue Value { get; set; }

        public string NameLanguageKey { get; } = null;

        public string Name
        {
            get => LanguageManager.GetString(NameLanguageKey) ?? Value?.ToString();
        }

        private void OnLanguageChanged(object _)
        {
            RaisePropertyChanged(nameof(Name));
        }

        public ItemViewModel()
        {
        }

        public ItemViewModel(TValue value)
        {
            Value = value;
        }

        public ItemViewModel(TValue value, string nameLanguageKey) : this(value)
        {
            NameLanguageKey = nameLanguageKey;
            Messenger.Global.Register(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
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
                if (NameLanguageKey != null)
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

    public static class ItemViewModel
    {
        public static int GetIndex<TValue>(IEnumerable<ItemViewModel<TValue>> list, TValue value)
        {
            return list.ToList().FindIndex(item => item.Value.Equals(value));
        }
    }
}
