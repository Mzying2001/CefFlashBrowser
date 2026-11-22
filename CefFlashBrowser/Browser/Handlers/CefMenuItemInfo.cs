using CefFlashBrowser.Browser.UserScripts;
using CefFlashBrowser.Models.UserScripts;
using CefSharp;
using System.Collections.Generic;

namespace CefFlashBrowser.Browser.Handlers
{
    internal class CefMenuItemInfo
    {
        public bool IsChecked { get; set; }
        public bool IsEnable { get; set; }
        public string Header { get; set; }
        public CefMenuCommand CommandID { get; set; }
        public bool IsUserScript { get; set; }
        public UserScriptMenuIcon ScriptIcon { get; set; }
        public IList<CefMenuItemInfo> SubMenuItemInfos { get; set; }



        public static List<CefMenuItemInfo> GetMenuItemInfoList(IMenuModel model, UserScriptContextMenuHandler scripts)
        {
            if (model == null)
            {
                return null;
            }

            List<CefMenuItemInfo> list = new List<CefMenuItemInfo>();

            for (var i = 0; i < model.Count; i++)
            {
                var commandId = model.GetCommandIdAt(i);
                UserScriptMenuIcon icon = null;
                var isUserScript = scripts?.TryGetScriptItem(commandId, out icon) == true;

                list.Add(new CefMenuItemInfo
                {
                    Header = model.GetLabelAt(i),
                    IsEnable = model.IsEnabledAt(i),
                    IsChecked = model.IsCheckedAt(i),
                    CommandID = commandId,
                    IsUserScript = isUserScript,
                    ScriptIcon = icon,
                    SubMenuItemInfos = GetMenuItemInfoList(model.GetSubMenuAt(i), scripts)
                });
            }

            return list;
        }
    }
}
