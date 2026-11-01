using CefFlashBrowser.Data;
using CefFlashBrowser.Infrastructure.Wpf;
using CefFlashBrowser.Utils;
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

        private Dictionary<string, ResourceDictionary> LanguageDictionaries { get; }

        public LanguageService()
        {
            LanguageDictionaries = new Dictionary<string, ResourceDictionary>();
        }

        public void InitLanguage()
        {
            LanguageDictionaries.Clear();

            var languages = new ResourceDictionary
            { Source = new Uri("Assets/Language/langs.xaml", UriKind.Relative) };

            foreach (DictionaryEntry entry in languages)
            {
                if (entry.Key is string lang && entry.Value is ResourceDictionary langDic)
                {
                    LanguageDictionaries.Add(lang, new NamedResourceDictionary(langDic)
                    {
                        Name = LanguageResourceDictionaryName
                    });
                }
            }

            SetCurrentLanguage(GlobalData.Settings.Language);
        }




        private ResourceDictionary GetCurLangResDic()
        {
            return ((App)Application.Current).GetNamedResourceDictionary(LanguageResourceDictionaryName);
        }

        private void SetCurLangResDic(ResourceDictionary dic)
        {
            var oldDic = GetCurLangResDic();

            foreach (var key in oldDic.Keys)
            {
                if (!dic.Contains(key))
                    dic[key] = oldDic[key];
            }

            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            mergedDictionaries[mergedDictionaries.IndexOf(oldDic)] = dic;
        }




        public string[] GetSupportedLanguage()
        {
            return (from item in LanguageDictionaries orderby GetLanguageName(item.Key) select item.Key).ToArray();
        }

        public bool IsSupportedLanguage(string language)
        {
            return language != null && LanguageDictionaries.ContainsKey(language);
        }

        public string GetCurrentLanguage()
        {
            var dic = GetCurLangResDic();
            return LanguageDictionaries.FirstOrDefault(pair => pair.Value == dic).Key; // null if not found
        }

        public void SetCurrentLanguage(string language)
        {
            if (!IsSupportedLanguage(language))
            {
                LogHelper.LogError($"Language '{language}' is not supported.");
            }
            else
            {
                SetCurLangResDic(LanguageDictionaries[language]);
                GlobalData.Settings.Language = language;
                Messenger.Global.Send(MessageTokens.LANGUAGE_CHANGED, language);
            }
        }




        public string GetString(string language, string key)
        {
            ResourceDictionary dic;

            if (IsSupportedLanguage(language))
            {
                dic = LanguageDictionaries[language];
            }
            else
            {
                dic = GetCurLangResDic();
                LogHelper.LogError($"Language '{language}' is not supported. Falling back to current language.");
            }

            return dic.Contains(key) ? dic[key].ToString() : string.Empty;
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
            var str = GetString(GlobalData.Settings.Language, key);
            return string.IsNullOrEmpty(str) ? str : string.Format(str, args);
        }
    }
}
