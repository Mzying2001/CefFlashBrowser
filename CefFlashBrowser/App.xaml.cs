using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using System.Windows;

namespace CefFlashBrowser
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (GlobalData.Settings.FirstStart)
            {
                if (WindowManager.ShowSelectLanguageDialog())
                {
                    GlobalData.Settings.FirstStart = false;
                    WindowManager.ShowMainWindow();
                }
                else
                {
                    Shutdown();
                    return;
                }
            }
            else if (e.Args.Length == 0)
            {
                WindowManager.ShowMainWindow();
            }
            else
            {
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

            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }
    }
}
