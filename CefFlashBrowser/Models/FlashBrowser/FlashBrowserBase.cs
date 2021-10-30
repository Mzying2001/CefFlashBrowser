using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using CefSharp.Wpf;
using SimpleMvvm.Command;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class FlashBrowserBase : ChromiumWebBrowser
    {
        public ICommand LoadUrlCommand { get; private set; }

        public ICommand ChangeLoadingStateCommand { get; private set; }

        public override void BeginInit()
        {
            base.BeginInit();

            LoadUrlCommand = new DelegateCommand(obj =>
            {
                if (obj is string url)
                    Address = url;
            });

            ChangeLoadingStateCommand = new DelegateCommand(() =>
            {
                if (IsLoading)
                    GetBrowser().StopLoad();
                else
                    GetBrowser().Reload();
            });

            DownloadHandler = new IEDownloadHandler();
            JsDialogHandler = new JsDialogHandler();
        }

        protected override void OnIsBrowserInitializedChanged(bool oldValue, bool newValue)
        {
            base.OnIsBrowserInitializedChanged(oldValue, newValue);

            if (newValue)
            {
                Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    var requestContext = GetBrowser().GetHost().RequestContext;
                    var flag = requestContext.SetPreference("profile.default_content_setting_values.plugins", 1, out string err);

                    if (!flag)
                    {
                        var title = LanguageManager.GetString("title_error");
                        Dispatcher.Invoke(() => JsAlertDialog.Show(err, title));
                    }
                });
            }
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

#if !DEBUG
            settings.LogSeverity = LogSeverity.Disable;
#endif

            const string FLASH_DLL_PATH = @"Assets\Plugins\flash.dll";

            settings.CefCommandLineArgs["enable-system-flash"] = "1";
            settings.CefCommandLineArgs.Add("ppapi-flash-version", FileVersionInfo.GetVersionInfo(FLASH_DLL_PATH).FileVersion.Replace(',', '.'));
            settings.CefCommandLineArgs.Add("ppapi-flash-path", FLASH_DLL_PATH);
            Cef.Initialize(settings);
        }
    }
}
