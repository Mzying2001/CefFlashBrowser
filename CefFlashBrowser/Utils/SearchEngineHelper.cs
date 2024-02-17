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
                    return $"www.baidu.com/s?wd={str}";

                case SearchEngine.Google:
                    return $"www.google.com/search?q={str}";

                case SearchEngine.Bing:
                    return $"www.bing.com/search?q={str}";

                case SearchEngine.Sogou:
                    return $"www.sogou.com/web?query={str}";

                case SearchEngine.So360:
                    return $"www.so.com/s?&q={str}";

                default:
                    throw new ArgumentException("Unknown search engine.", nameof(engine));
            }
        }
    }
}
