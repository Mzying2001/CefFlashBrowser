using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
using System;
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
            LanguageDictionaries = new Dictionary<string, ResourceDictionary>
            {
                ["zh-CN"] = new ResourceDictionary() { Source = new Uri("Assets/Language/zh-CN.xaml", UriKind.Relative) },
                ["zh-TW"] = new ResourceDictionary() { Source = new Uri("Assets/Language/zh-TW.xaml", UriKind.Relative) },
                ["en-US"] = new ResourceDictionary() { Source = new Uri("Assets/Language/en-US.xaml", UriKind.Relative) },
                ["it"] = new ResourceDictionary() { Source = new Uri("Assets/Language/it.xaml", UriKind.Relative) },
            };

            CurrentLanguage = GlobalData.Settings.Language;
        }




        private static int GetLangResDicIndex()
        {
            var dics = Application.Current.Resources.MergedDictionaries;
            for (int i = 0; i < dics.Count; i++)
            {
                if (dics[i].Source.ToString().StartsWith("Assets/Language/"))
                {
                    return i;
                }
            }
            return -1;
        }

        private static ResourceDictionary GetCurLangResDic()
        {
            int index = GetLangResDicIndex();
            return index != -1 ? Application.Current.Resources.MergedDictionaries[index] : null;
        }




        public static IEnumerable<string> GetSupportedLanguage()
        {
            return from item in LanguageDictionaries orderby GetLanguageName(item.Key) select item.Key;
        }

        public static bool IsSupportedLanguage(string language)
        {
            return LanguageDictionaries.ContainsKey(language);
        }

        public static string CurrentLanguage
        {
            get
            {
                string url = GetCurLangResDic().Source.ToString();
                return System.IO.Path.GetFileNameWithoutExtension(url);
            }
            set
            {
                int index = GetLangResDicIndex();
                if (index != -1 && IsSupportedLanguage(value))
                {
                    Application.Current.Resources.MergedDictionaries[index] = LanguageDictionaries[value];
                    GlobalData.Settings.Language = value;
                    Messenger.Global.Send(MessageTokens.LANGUAGE_CHANGED, value);
                }
            }
        }




        public static string GetString(string language, string key)
        {
            if (IsSupportedLanguage(language))
            {
                var dic = LanguageDictionaries[language];
                return dic[key].ToString();
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
