using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace CefFlashBrowser
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static bool _restart = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitCefFlash();

            if (e.Args.Length == 0)
            {
                Views.MainWindow.Show();
                return;
            }

            foreach (var arg in e.Args)
            {
                if (UrlChecker.IsLocalSwfFile(arg))
                {
                    SwfPlayerWindow.Show(arg);
                }
                else
                {
                    BrowserWindow.Show(arg);
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            GlobalData.SaveData();

            if (_restart)
            {
                Process.Start(Process.GetCurrentProcess().MainModule.FileName);
            }
        }

        public static void Restart()
        {
            _restart = true;
            Current.Shutdown();
        }


        /*====================================================================================================*/


        public static readonly string BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string EmptyExePath = Path.Combine(BaseDirectory, @"CefFlashBrowser.EmptyExe.exe");
        public static readonly string CachePath = Path.Combine(BaseDirectory, @"Caches\");
        public static readonly string FlashPath = Path.Combine(BaseDirectory, @"Assets\Plugins\pepflashplayer.dll");

        private static void InitCefFlash()
        {
            Environment.SetEnvironmentVariable("ComSpec", EmptyExePath); //Remove black popup window

            CefSettings settings = new CefSettings()
            {
                Locale = GlobalData.Settings.Language,
                CachePath = CachePath
            };

            if (GlobalData.Settings.UserAgentSetting.EnableCustom)
            {
                settings.UserAgent = GlobalData.Settings.UserAgentSetting.UserAgent;
            }

            if (GlobalData.Settings.ProxySettings.EnableProxy)
            {
                var proxySettings = GlobalData.Settings.ProxySettings;
                CefSharpSettings.Proxy = new ProxyOptions(proxySettings.IP, proxySettings.Port, proxySettings.UserName, proxySettings.Password);
            }

#if !DEBUG
            settings.LogSeverity = LogSeverity.Disable;
#endif

            settings.CefCommandLineArgs["enable-system-flash"] = "1";
            settings.CefCommandLineArgs["ppapi-flash-path"] = FlashPath;
            settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";

            if (GlobalData.Settings.FakeFlashVersionSetting.Enable)
            {
                settings.CefCommandLineArgs["ppapi-flash-version"] = GlobalData.Settings.FakeFlashVersionSetting.FlashVersion;
            }
            else
            {
                settings.CefCommandLineArgs["ppapi-flash-version"] = FileVersionInfo.GetVersionInfo(FlashPath).FileVersion.Replace(',', '.');
            }

            Cef.Initialize(settings);
        }

    }
}
