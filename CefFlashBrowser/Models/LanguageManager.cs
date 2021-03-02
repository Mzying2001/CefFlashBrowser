using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Models
{
    static class LanguageManager
    {
        public static readonly string[] SupportedLanguage =
        {
            "en-us",
            "zh-cn"
        };

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
            return new Uri($"Languages\\{language}.xaml", UriKind.Relative);
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
                    Settings.Language = value;
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
    }
}
