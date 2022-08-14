using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Messaging;
using System.Collections.Generic;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class ItemViewModel<TValue> : ViewModelBase
    {
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
            if (NameLanguageKey != null)
            {
                Messenger.Global.Unregister(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
            }
        }
    }

    public static class ItemViewModel
    {
        public static int GetIndex<TValue>(IEnumerable<ItemViewModel<TValue>> list, TValue value)
        {
            return list.Select(item => item.Value).ToList().IndexOf(value);
        }
    }
}
