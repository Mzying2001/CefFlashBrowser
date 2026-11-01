using CefFlashBrowser.Data;
using CefFlashBrowser.Infrastructure.Wpf;
using CefFlashBrowser.Log;
using SimpleMvvm.Messaging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Services
{
    public class LanguageService
    {
        private const string LanguageResourceDictionaryName = "Language";

        private readonly ILogger _logger;
        private readonly Dictionary<string, ResourceDictionary> _languageDictionaries = new Dictionary<string, ResourceDictionary>();
        private string _currentLanguage;

        public LanguageService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            var languages = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/CefFlashBrowser;component/Assets/Language/langs.xaml")
            };

            foreach (DictionaryEntry entry in languages)
            {
                if (entry.Key is string language && entry.Value is ResourceDictionary dictionary)
                {
                    _languageDictionaries.Add(language, new NamedResourceDictionary(dictionary)
                    {
                        Name = LanguageResourceDictionaryName
                    });
                }
            }

            SetCurrentLanguage(GlobalData.Settings.Language);
        }

        private ResourceDictionary GetCurrentLanguageDictionary()
        {
            return Application.Current.Resources.MergedDictionaries
                .OfType<NamedResourceDictionary>()
                .FirstOrDefault(dictionary => dictionary.Name == LanguageResourceDictionaryName);
        }

        private void SetCurrentLanguageDictionary(ResourceDictionary dictionary)
        {
            var oldDictionary = GetCurrentLanguageDictionary();

            foreach (var key in oldDictionary.Keys)
            {
                if (!dictionary.Contains(key))
                    dictionary[key] = oldDictionary[key];
            }

            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            mergedDictionaries[mergedDictionaries.IndexOf(oldDictionary)] = dictionary;
        }

        public string[] GetSupportedLanguage()
        {
            return _languageDictionaries.Keys.OrderBy(GetLanguageName).ToArray();
        }

        public bool IsSupportedLanguage(string language)
        {
            return language != null && _languageDictionaries.ContainsKey(language);
        }

        public string GetCurrentLanguage()
        {
            return _currentLanguage;
        }

        public void SetCurrentLanguage(string language)
        {
            if (language == null || !_languageDictionaries.TryGetValue(language, out var dictionary))
            {
                _logger.Log(LogLevel.Error, $"Language '{language}' is not supported.");
                return;
            }

            SetCurrentLanguageDictionary(dictionary);
            _currentLanguage = language;
            GlobalData.Settings.Language = language;
            Messenger.Global.Send(MessageTokens.LANGUAGE_CHANGED, language);
        }

        public string GetString(string language, string key)
        {
            if (language == null || !_languageDictionaries.TryGetValue(language, out var dictionary))
            {
                dictionary = GetCurrentLanguageDictionary();
                _logger.Log(LogLevel.Error, $"Language '{language}' is not supported. Falling back to current language.");
            }

            return dictionary.Contains(key) ? dictionary[key].ToString() : string.Empty;
        }

        public string GetString(string key)
        {
            return GetString(GlobalData.Settings.Language, key);
        }

        public string GetLanguageName(string language)
        {
            return GetString(language, "language_name");
        }

        public string GetLocale(string language)
        {
            return GetString(language, "locale");
        }

        public string GetFormattedString(string key, params object[] args)
        {
            var str = GetString(key);
            return string.IsNullOrEmpty(str) ? str : string.Format(str, args);
        }
    }
}
