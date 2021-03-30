using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using CefSharp;
using CefSharp.Wpf;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class ChromiumFlashBrowser : ChromiumWebBrowser
    {
        public event EventHandler<NewWindowEventArgs> OnCreatedNewWindow;

        public ChromiumFlashBrowser() : base()
        {
            InitBrowser();
        }

        public ChromiumFlashBrowser(string initialAddress) : base(initialAddress)
        {
            InitBrowser();
        }

        public ChromiumFlashBrowser(HwndSource parentWindowHwndSource, string initialAddress, Size size) : base(parentWindowHwndSource, initialAddress, size)
        {
            InitBrowser();
        }

        /// <summary>
        /// This method should be called when the program starts
        /// </summary>
        public static void InitCefFlash()
        {
            if (Cef.IsInitialized)
                return;

            CefSettings settings = new CefSettings()
            {
                Locale = Settings.Language,
                CachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"caches\")
            };

            settings.CefCommandLineArgs["enable-system-flash"] = "1";
            settings.CefCommandLineArgs.Add("ppapi-flash-version", "32.34.0.0.92");
            settings.CefCommandLineArgs.Add("ppapi-flash-path", @"plugins\pepflashplayer32_34_0_0_92.dll");
            Cef.Initialize(settings);
        }

        private void InitBrowser()
        {
            EnableFlash();
            DownloadHandler = new IEDownloadHandler();
            LifeSpanHandler = new FlashBrowserLifeSpanHandler((s, e) =>
            {
                OnCreatedNewWindow?.Invoke(s, e);
            });
        }

        private void EnableFlash()
        {
            IsBrowserInitializedChanged += (s, e) =>
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
