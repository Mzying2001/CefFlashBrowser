using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models
{
    class SearchEngine
    {
        public Engines Engine { get; set; }

        public enum Engines
        {
            Baidu,
            Bing,
            So360,
            Sougou
        }

        public SearchEngine(Engines e)
        {
            Engine = e;
        }

        public static string GetUrl(string str, Engines e = Engines.Baidu)
        {
            switch (e)
            {
                case Engines.Baidu:
                    return $"www.baidu.com/s?wd={str}";

                case Engines.Bing:
                    return $"bing.com/search?q={str}";

                case Engines.Sougou:
                    return $"www.sogou.com/web?query={str}";

                case Engines.So360:
                    return $"www.so.com/s?&q={str}";

                default:
                    throw new Exception("Unknown search engine.");
            }
        }

        public string GetUrl(string str)
        {
            return GetUrl(str, Engine);
        }
    }
}
