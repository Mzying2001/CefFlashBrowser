using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using System;
using System.Diagnostics;
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

            if (GlobalData.Settings.FirstStart)
            {
                if (WindowManager.ShowSelectLanguageDialog())
                    GlobalData.Settings.FirstStart = false;
                return;
            }

            if (e.Args.Length == 0)
            {
                WindowManager.ShowMainWindow();
                return;
            }

            GlobalData.IsStartWithoutMainWindow = true;
            foreach (var arg in e.Args)
            {
                if (UrlChecker.IsLocalSwfFile(arg))
                {
                    WindowManager.ShowSwfPlayer(arg);
                }
                else
                {
                    WindowManager.ShowBrowser(arg);
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


        private static void InitCefFlash()
        {
            Environment.SetEnvironmentVariable("ComSpec", GlobalData.EmptyExePath); //Remove black popup window

            var settings = new CefFlashSettings()
            {
                Locale = LanguageManager.GetLocale(GlobalData.Settings.Language),
                CachePath = GlobalData.CachesPath,
                PpapiFlashPath = GlobalData.FlashPath,
                EnableSystemFlash = true
            };

            if (GlobalData.Settings.FakeFlashVersionSetting.Enable)
            {
                settings.PpapiFlashVersion = GlobalData.Settings.FakeFlashVersionSetting.FlashVersion;
            }
            else
            {
                settings.PpapiFlashVersion = FileVersionInfo.GetVersionInfo(GlobalData.FlashPath).FileVersion.Replace(',', '.');
            }

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

            settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";
            Cef.Initialize(settings);
        }

    }
}
