using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Models
{
    static class SearchEngine
    {
        public enum Engines
        {
            Baidu,
            Bing,
            So360,
            Sogou
        }

        public static string GetUrl(string str, Engines e = Engines.Baidu)
        {
            switch (e)
            {
                case Engines.Baidu:
                    return $"www.baidu.com/s?wd={str}";

                case Engines.Bing:
                    return $"bing.com/search?q={str}";

                case Engines.Sogou:
                    return $"www.sogou.com/web?query={str}";

                case Engines.So360:
                    return $"www.so.com/s?&q={str}";

                default:
                    throw new Exception("Unknown search engine.");
            }
        }

        public static IEnumerable<(Engines engine, string name)> GetSupportedSearchEngines()
        {
            var res = Application.Current.Resources.MergedDictionaries[0];

            yield return (Engines.Baidu, res["baidu"].ToString());
            yield return (Engines.Bing, res["bing"].ToString());
            yield return (Engines.So360, res["so360"].ToString());
            yield return (Engines.Sogou, res["sogou"].ToString());
        }
    }
}
