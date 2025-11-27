using CefFlashBrowser.Models.Data;
using System;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class EmoticonsHelper
    {
        [ThreadStatic]
        private static int _next = 0;

        private static string[] Emoticons { get; }

        static EmoticonsHelper()
        {
            var dic = new ResourceDictionary
            { Source = new Uri("Assets/Emoticons/Emoticons.xaml", UriKind.Relative) };

            var r = new Random();
            Emoticons = ((string[])dic["Emoticons"]).OrderBy(item => r.Next()).ToArray();
        }

        public static string GetNextEmoticon()
        {
            if (GlobalData.Settings.CustomEmoticon is string custom)
            {
                return custom;
            }
            else
            {
                _next %= Emoticons.Length;
                return Emoticons[_next++];
            }
        }
    }
}
