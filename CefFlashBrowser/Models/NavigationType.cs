using System.Collections.Generic;

namespace CefFlashBrowser.Models
{
    public static class NavigationType
    {
        public enum Type
        {
            Automatic,
            SearchOnly,
            NavigateOnly
        }

        public static IEnumerable<EnumDescription<Type>> GetNavigationTypes()
        {
            yield return new EnumDescription<Type>(Type.Automatic, LanguageManager.GetString("navigationType_auto"));
            yield return new EnumDescription<Type>(Type.SearchOnly, LanguageManager.GetString("navigationType_searchOnly"));
            yield return new EnumDescription<Type>(Type.NavigateOnly, LanguageManager.GetString("navigationType_navigateOnly"));
        }
    }
}
