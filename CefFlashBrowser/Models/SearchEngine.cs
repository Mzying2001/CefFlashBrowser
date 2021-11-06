using System;
using System.Collections.Generic;

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

        public static IEnumerable<EnumDescription<Engine>> GetSupportedSearchEngines()
        {
            yield return new EnumDescription<Engine>(Engine.Baidu, LanguageManager.GetString("baidu"));
            yield return new EnumDescription<Engine>(Engine.Google, LanguageManager.GetString("google"));
            yield return new EnumDescription<Engine>(Engine.Bing, LanguageManager.GetString("bing"));
            yield return new EnumDescription<Engine>(Engine.Sogou, LanguageManager.GetString("sogou"));
            yield return new EnumDescription<Engine>(Engine.So360, LanguageManager.GetString("so360"));
        }
    }
}
