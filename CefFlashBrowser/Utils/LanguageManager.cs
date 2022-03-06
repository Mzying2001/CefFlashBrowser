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
        private static readonly string[] SupportedLanguage =
        {
            "en-US",
            "zh-CN",
            "zh-TW"
        };

        public static IEnumerable<string> GetSupportedLanguage()
        {
            return from item in SupportedLanguage orderby GetLanguageName(item) select item;
        }

        public static bool IsSupportedLanguage(string language)
        {
            return SupportedLanguage.Contains(language);
        }

        private static ResourceDictionary LanguageResourceDic
        {
            get => Application.Current.Resources.MergedDictionaries[0];
        }

        private static Uri GetUri(string language)
        {
            return new Uri($"Assets\\Language\\{language}.xaml", UriKind.Relative);
        }

        public static string CurrentLanguage
        {
            get
            {
                string url = LanguageResourceDic.Source.ToString();
                return url.Substring(url.LastIndexOf('\\') + 1, url.LastIndexOf('.') - url.LastIndexOf('\\') - 1);
            }
            set
            {
                if (IsSupportedLanguage(value))
                {
                    LanguageResourceDic.Source = GetUri(value);
                    GlobalData.Settings.Language = value;
                    Messenger.Global.Send(MessageTokens.LANGUAGE_CHANGED, value);
                }
            }
        }

        public static string GetLanguageName(string language)
        {
            if (IsSupportedLanguage(language))
                return new ResourceDictionary() { Source = GetUri(language) }["language_name"].ToString();
            else
                throw new Exception("Unsupported language.");
        }

        public static string GetString(string key)
        {
            return Application.Current.Resources.MergedDictionaries[0][key].ToString();
        }

        public static void InitLanguage()
        {
            CurrentLanguage = GlobalData.Settings.Language;
        }
    }
}
