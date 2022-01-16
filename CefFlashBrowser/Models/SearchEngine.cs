using System;
using System.Net;

namespace CefFlashBrowser.Models
{
    public static class SearchEngine
    {
        public enum Engine
        {
            Baidu,
            Google,
            Bing,
            Sogou,
            So360,
        }

        public static string GetUrl(string str, Engine e = Engine.Baidu)
        {
            str = WebUtility.UrlEncode(str);

            switch (e)
            {
                case Engine.Baidu:
                    return $"www.baidu.com/s?wd={str}";

                case Engine.Google:
                    return $"www.google.com/search?q={str}";

                case Engine.Bing:
                    return $"bing.com/search?q={str}";

                case Engine.Sogou:
                    return $"www.sogou.com/web?query={str}";

                case Engine.So360:
                    return $"www.so.com/s?&q={str}";

                default:
                    throw new Exception("Unknown search engine.");
            }
        }
    }
}
