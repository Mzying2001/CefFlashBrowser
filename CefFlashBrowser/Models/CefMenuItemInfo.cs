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
    }
}
