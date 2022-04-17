using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
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

        private void Init()
        {
            GlobalData.InitData();
            LanguageManager.InitLanguage();
            FlashBrowserBase.InitCefFlash();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Init();

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
    }
}
