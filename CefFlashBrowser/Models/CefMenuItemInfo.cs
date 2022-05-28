using CefSharp;
using System.Collections.Generic;

namespace CefFlashBrowser.Models
{
    public class CefMenuItemInfo
    {
        public bool IsChecked { get; set; }
        public bool IsEnable { get; set; }
        public string Header { get; set; }
        public CefMenuCommand CommandID { get; set; }
        public IList<CefMenuItemInfo> SubMenuItemInfos { get; set; }



        public static List<CefMenuItemInfo> GetMenuItemInfoList(IMenuModel model)
        {
            if (model == null)
            {
                return null;
            }

            List<CefMenuItemInfo> list = new List<CefMenuItemInfo>();

            for (var i = 0; i < model.Count; i++)
            {
                list.Add(new CefMenuItemInfo
                {
                    Header = model.GetLabelAt(i),
                    IsEnable = model.IsEnabledAt(i),
                    IsChecked = model.IsCheckedAt(i),
                    CommandID = model.GetCommandIdAt(i),
                    SubMenuItemInfos = GetMenuItemInfoList(model.GetSubMenuAt(i))
                });
            }

            return list;
        }
    }
}
