using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CefFlashBrowser.Utils
{
    public static class UrlHelper
    {
        private static readonly Regex _httpUrlRegex
            = new Regex(@"^(https?://)?[\w\-%]+(\.[\w\-%]+)+(:\d+)?(/[\w\-\.%]+)*/?(\?[^?&=]+(=[^?&=]+)?(&[^?&=]+(=[^?&=]+)?)*)?$");

        public static bool IsHttpUrl(string url)
        {
            return !double.TryParse(url, out _) && _httpUrlRegex.IsMatch(url);
        }

        public static bool IsLocalSwfFile(string url)
        {
            return Path.GetExtension(url).Equals(".swf", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
        }

        public static bool IsLocalSolFile(string url)
        {
            return Path.GetExtension(url).Equals(".sol", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
        }
    }
}
