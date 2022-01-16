using System.Collections.Generic;

namespace CefFlashBrowser.Models
{
    public static class EnumDescriptions
    {
        public static IEnumerable<EnumDescription<SearchEngine.Engine>> GetSearchEngines()
        {
            yield return new EnumDescription<SearchEngine.Engine>(SearchEngine.Engine.Baidu, LanguageManager.GetString("baidu"));
            yield return new EnumDescription<SearchEngine.Engine>(SearchEngine.Engine.Google, LanguageManager.GetString("google"));
            yield return new EnumDescription<SearchEngine.Engine>(SearchEngine.Engine.Bing, LanguageManager.GetString("bing"));
            yield return new EnumDescription<SearchEngine.Engine>(SearchEngine.Engine.Sogou, LanguageManager.GetString("sogou"));
            yield return new EnumDescription<SearchEngine.Engine>(SearchEngine.Engine.So360, LanguageManager.GetString("so360"));
        }

        public static IEnumerable<EnumDescription<NavigationType>> GetNavigationTypes()
        {
            yield return new EnumDescription<NavigationType>(NavigationType.Automatic, LanguageManager.GetString("navigationType_auto"));
            yield return new EnumDescription<NavigationType>(NavigationType.SearchOnly, LanguageManager.GetString("navigationType_searchOnly"));
            yield return new EnumDescription<NavigationType>(NavigationType.NavigateOnly, LanguageManager.GetString("navigationType_navigateOnly"));
        }

        public static IEnumerable<EnumDescription<NewPageBehavior>> GetNewPageBehaviors()
        {
            yield return new EnumDescription<NewPageBehavior>(NewPageBehavior.OriginalWindow, LanguageManager.GetString("newPageBehavior_originalWindow"));
            yield return new EnumDescription<NewPageBehavior>(NewPageBehavior.NewWindow, LanguageManager.GetString("newPageBehavior_newWindow"));
        }
    }
}
