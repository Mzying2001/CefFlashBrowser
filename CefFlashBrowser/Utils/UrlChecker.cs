using System.IO;
using System.Text.RegularExpressions;

namespace CefFlashBrowser.Utils
{
    public static class UrlChecker
    {
        private static readonly Regex _httpUrlRegex
            = new Regex(@"^(https?://)?(\w|\d|-)+(\.(\w|\d|-)+)+(:\d+)?(/(\w|\d|-)+)*(/((\w|\d|-)+\.(\w|\d|-)+)?)?(\?[^?&=]+=[^?&=]+(&[^?&=]+=[^?&=]+)*)?$");

        public static bool IsHttpUrl(string url)
        {
            return !double.TryParse(url, out _) && _httpUrlRegex.IsMatch(url);
        }

        public static bool IsLocalSwfFile(string url)
        {
            return url.ToLower().EndsWith(".swf") && File.Exists(url);
        }
    }
}
