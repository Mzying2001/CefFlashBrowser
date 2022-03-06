using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using System;
using System.Windows;

namespace CefFlashBrowser
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private void Init()
        {
            GlobalData.InitData();
            LanguageManager.InitLanguage();
            FlashBrowserBase.InitCefFlash();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Init();
            base.OnStartup(e);

            if (e.Args.Length == 0)
            {
                new MainWindow().Show();
                return;
            }

            foreach (var arg in e.Args)
            {
                if (UrlChecker.IsLocalSwfFile(arg))
                {
                    SwfPlayerWindow.Show(arg);
                }
                else if (UrlChecker.IsHttpUrl(arg))
                {
                    BrowserWindow.Show(arg);
                }
                else
                {
                    JsAlertDialog.ShowDialog($"{LanguageManager.GetString("invalidStartUpParam")}: {arg}");
                    Environment.Exit(0);
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            GlobalData.SaveData();
            base.OnExit(e);
        }
    }
}
