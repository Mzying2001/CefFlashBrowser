using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class LanguageManager
    {
        private static Dictionary<string, ResourceDictionary> LanguageDictionaries { get; }

        static LanguageManager()
        {
            LanguageDictionaries = new Dictionary<string, ResourceDictionary>();
        }

        public static void InitLanguage()
        {
            LanguageDictionaries.Clear();

            var languages = new ResourceDictionary
            { Source = new Uri("Assets/Language/langs.xaml", UriKind.Relative) };

            foreach (DictionaryEntry entry in languages)
            {
                if (entry.Key is string lang && entry.Value is ResourceDictionary langDic)
                {
                    LanguageDictionaries.Add(lang, langDic);
                }
            }

            CurrentLanguage = GlobalData.Settings.Language;
        }




        private static ResourceDictionary GetCurLangResDic()
        {
            return Application.Current.Resources.MergedDictionaries[0];
        }

        private static void SetCurLangResDic(ResourceDictionary dic)
        {
            var oldDic = GetCurLangResDic();
            foreach (var key in oldDic.Keys)
            {
                if (!dic.Contains(key))
                    dic[key] = oldDic[key];
            }

            Application.Current.Resources.MergedDictionaries[0] = dic;
        }




        public static string[] GetSupportedLanguage()
        {
            return (from item in LanguageDictionaries orderby GetLanguageName(item.Key) select item.Key).ToArray();
        }

        public static bool IsSupportedLanguage(string language)
        {
            return LanguageDictionaries.ContainsKey(language);
        }

        public static string CurrentLanguage
        {
            get
            {
                var curDic = GetCurLangResDic();
                foreach (var pair in LanguageDictionaries)
                {
                    if (pair.Value == curDic) return pair.Key;
                }
                return null;
            }
            set
            {
                if (IsSupportedLanguage(value))
                {
                    SetCurLangResDic(LanguageDictionaries[value]);
                    GlobalData.Settings.Language = value;
                    Messenger.Global.Send(MessageTokens.LANGUAGE_CHANGED, value);
                }
            }
        }




        public static string GetString(string language, string key)
        {
            if (key != null && IsSupportedLanguage(language))
            {
                var dic = LanguageDictionaries[language];
                return dic.Contains(key) ? dic[key].ToString() : string.Empty;
            }
            else
            {
                return null;
            }
        }

        public static string GetString(string key)
        {
            return GetString(GlobalData.Settings.Language, key);
        }

        public static string GetLanguageName(string language)
        {
            return GetString(language, "language_name");
        }

        public static string GetLocale(string language)
        {
            return GetString(language, "locale");
        }
    }
}
