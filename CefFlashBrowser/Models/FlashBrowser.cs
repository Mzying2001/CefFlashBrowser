using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using CefSharp;
using CefSharp.Wpf;

namespace CefFlashBrowser.Models
{
    public class FlashBrowser : ChromiumWebBrowser
    {
        public FlashBrowser() : base()
        {
            EnableFlash();
        }

        public FlashBrowser(string initialAddress) : base(initialAddress)
        {
            EnableFlash();
        }

        public FlashBrowser(HwndSource parentWindowHwndSource, string initialAddress, Size size) : base(parentWindowHwndSource, initialAddress, size)
        {
            EnableFlash();
        }

        public static void InitCefFlash()
        {
            CefSettings settings = new CefSettings();
            settings.CefCommandLineArgs["enable-system-flash"] = "1";
            settings.CefCommandLineArgs.Add("ppapi-flash-version", "32.34.0.0.92");
            settings.CefCommandLineArgs.Add("ppapi-flash-path", @"plugins\pepflashplayer32_34_0_0_92.dll");
            Cef.Initialize(settings);
        }

        private void EnableFlash()
        {
            IsBrowserInitializedChanged += (sender, e) =>
            {
                if (!IsBrowserInitialized)
                    return;

                Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    var requestContext = GetBrowser().GetHost().RequestContext;
                    requestContext.SetPreference("profile.default_content_setting_values.plugins", 1, out string error);
                });
            };
        }
    }
}
