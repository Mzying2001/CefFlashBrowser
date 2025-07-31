using CefFlashBrowser.Models;
using System;
using System.Net;

namespace CefFlashBrowser.Utils
{
    public static class SearchEngineHelper
    {
        public static string GetUrl(string str, SearchEngine engine)
        {
            str = WebUtility.UrlEncode(str);

            switch (engine)
            {
                case SearchEngine.Baidu:
                    return $"https://www.baidu.com/s?wd={str}";

                case SearchEngine.Google:
                    return $"https://www.google.com/search?q={str}";

                case SearchEngine.Bing:
                    return $"https://www.bing.com/search?q={str}";

                case SearchEngine.Sogou:
                    return $"https://www.sogou.com/web?query={str}";

                case SearchEngine.So360:
                    return $"https://www.so.com/s?&q={str}";

                case SearchEngine.DuckDuckGo:
                    return $"https://duckduckgo.com/?q={str}";

                case SearchEngine.Yandex:
                    return $"https://yandex.com/search/?text={str}";

                case SearchEngine.Bilibili:
                    return $"https://search.bilibili.com/all?keyword={str}";

                case SearchEngine.Game4399:
                    return $"https://so2.4399.com/search/search.php?k={str}";

                default:
                    throw new ArgumentException("Unknown search engine.", nameof(engine));
            }
        }
    }
}
