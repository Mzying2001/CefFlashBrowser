using CefFlashBrowser.Models;
using System;
using System.IO;
using System.Linq;
using System.Net;

namespace CefFlashBrowser.Utils
{
    public static class UrlHelper
    {
        /// <summary>
        /// Determines whether the input string looks like a URL (similar to Chrome's address bar behavior).
        /// Returns true for URLs with explicit schemes, localhost, IP addresses, and domain-like strings.
        /// </summary>
        public static bool IsUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // 1. Pure numbers are not URLs
            if (input.All(char.IsDigit))
                return false;

            // 2. Has explicit scheme (e.g. http://..., https://..., custom://...)
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                return input.Contains("://")
                    && uri.Scheme != Uri.UriSchemeFile;
            }

            // 3. Try prepending http:// and check if it forms a valid URL
            if (Uri.TryCreate("http://" + input, UriKind.Absolute, out uri))
            {
                var host = uri.Host;

                // localhost (with optional port)
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    return true;

                // Domain-like: contains dot and is not a plain number (e.g. "3.14")
                if (host.Contains(".") && !double.TryParse(input, out _))
                    return true;
            }

            return false;
        }

        public static bool IsLocalSwfFile(string url)
        {
            return Path.GetExtension(url).Equals(".swf", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
        }

        public static bool IsLocalSolFile(string url)
        {
            return Path.GetExtension(url).Equals(".sol", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
        }

        public static string GetSearchUrl(SearchEngine engine, string keyword)
        {
            var encoded = WebUtility.UrlEncode(keyword);

            switch (engine)
            {
                case SearchEngine.Baidu:
                    return $"https://www.baidu.com/s?wd={encoded}";

                case SearchEngine.Google:
                    return $"https://www.google.com/search?q={encoded}";

                case SearchEngine.Bing:
                    return $"https://www.bing.com/search?q={encoded}";

                case SearchEngine.Sogou:
                    return $"https://www.sogou.com/web?query={encoded}";

                case SearchEngine.So360:
                    return $"https://www.so.com/s?&q={encoded}";

                case SearchEngine.DuckDuckGo:
                    return $"https://duckduckgo.com/?q={encoded}";

                case SearchEngine.Yandex:
                    return $"https://yandex.com/search/?text={encoded}";

                case SearchEngine.Bilibili:
                    return $"https://search.bilibili.com/all?keyword={encoded}";

                case SearchEngine.Game4399:
                    return $"https://so2.4399.com/search/search.php?k={encoded}";

                default:
                    throw new ArgumentException(LanguageManager.GetString("error_unknownSearchEngine"), nameof(engine));
            }
        }
    }
}
