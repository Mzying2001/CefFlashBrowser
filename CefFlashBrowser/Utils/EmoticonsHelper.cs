using System;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class EmoticonsHelper
    {
        private static string[] Emoticons { get; }

        private static int Next { get; set; }

        static EmoticonsHelper()
        {
            var dic = new ResourceDictionary
            { Source = new Uri("Assets/Emoticons/Emoticons.xaml", UriKind.Relative) };

            var r = new Random();
            Emoticons = ((string[])dic["Emoticons"]).OrderBy(item => r.Next()).ToArray();
        }

        public static string GetNextEmoticon()
        {
            Next %= Emoticons.Length;
            return Emoticons[Next++];
        }
    }
}
