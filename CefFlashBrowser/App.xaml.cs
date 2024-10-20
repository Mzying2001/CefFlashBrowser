using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
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

            try
            {
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
                    if (UrlHelper.IsLocalSwfFile(arg))
                    {
                        WindowManager.ShowSwfPlayer(arg);
                    }
                    else
                    {
                        WindowManager.ShowBrowser(arg);
                    }
                }
            }
            catch (Exception ex)
            {
                WindowManager.Alert(ex.ToString(), LanguageManager.GetString("title_error"));
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
