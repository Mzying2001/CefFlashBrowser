using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CefFlashBrowser
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Win32.SetDllDirectory(GlobalData.CefDllPath);
            AppDomain.CurrentDomain.AssemblyResolve += ResolveCefSharpAssembly;

            try
            {
                var app = new App();
                app.InitializeComponent();

                InitCefFlash();
                app.Run();
            }
            catch (Exception e)
            {
                WindowManager.Alert(e.ToString(), LanguageManager.GetString("title_error"));
            }
        }

        static Assembly ResolveCefSharpAssembly(object sender, ResolveEventArgs e)
        {
            // Load CefSharp dlls
            string assemblyName = new AssemblyName(e.Name).Name;
            string assemblyPath = Path.Combine(GlobalData.CefDllPath, assemblyName + ".dll");
            return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
        }

        static void InitCefFlash()
        {
            Environment.SetEnvironmentVariable("ComSpec", GlobalData.EmptyExePath); //Remove black popup window

            var settings = new CefFlashSettings()
            {
                Locale = LanguageManager.GetLocale(GlobalData.Settings.Language),
                CachePath = GlobalData.CachesPath,
                PpapiFlashPath = GlobalData.FlashPath,
                EnableSystemFlash = true,
                BrowserSubprocessPath = GlobalData.SubprocessPath
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
