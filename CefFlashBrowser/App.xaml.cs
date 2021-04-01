using CefFlashBrowser.Models;
using CefFlashBrowser.Models.FlashBrowser;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            Settings.Init();
            ChromiumFlashBrowser.InitCefFlash();
            LanguageManager.InitLanguage();
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
                if (File.Exists(arg) && arg.ToLower().EndsWith(".swf"))
                {
                    BrowserWindow.PopupFlashPlayer(arg);
                }
                else if (UrlChecker.IsUrl(arg))
                {
                    BrowserWindow.Popup(arg);
                }
                else
                {
                    MessageBox.Show($"{LanguageManager.GetString("invalidStartUpParam")}: {arg}");
                    Environment.Exit(1);
                }
            }
        }
    }
}
