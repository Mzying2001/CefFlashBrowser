using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models.StaticData
{
    static class Settings
    {
        private static RegistryItem _reg;

        public static void Init()
        {
            _reg = new RegistryItem(Registry.CurrentUser, @"SOFTWARE\CefFlashBrowser");
        }

        public static string Language
        {
            get => _reg.GetValue<string>("Language") ?? "en-US";
            set => _reg["Language"] = value;
        }

        public static SearchEngine.Engine SearchEngine
        {
            get => (SearchEngine.Engine)_reg.GetValue<int>("SearchEngine");
            set => _reg["SearchEngine"] = (int)value;
        }

        public static int AddressBarFunction
        {
            get => _reg.GetValue<int>("AddressBarFunction");
            set => _reg["AddressBarFunction"] = value;
        }

        public static double BrowserWindowX
        {
            get => _reg.GetValue<double>("BrowserWindowX");
            set => _reg["BrowserWindowX"] = value;
        }

        public static double BrowserWindowY
        {
            get => _reg.GetValue<double>("BrowserWindowY");
            set => _reg["BrowserWindowY"] = value;
        }

        public static double BrowserWindowWidth
        {
            get => _reg.GetValue<double>("BrowserWindowWidth");
            set => _reg["BrowserWindowWidth"] = value;
        }

        public static double BrowserWindowHeight
        {
            get => _reg.GetValue<double>("BrowserWindowHeight");
            set => _reg["BrowserWindowHeight"] = value;
        }

        public static bool NotFirstStart
        {
            get => _reg.GetValue<bool>("NotFirstStart");
            set => _reg["NotFirstStart"] = value;
        }
    }
}
