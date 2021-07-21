using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels.SettingPanelViewModels
{
    class BrowserSettingPanelViewModel : NotificationObject
    {
        public ICommand DeleteCacheCommand { get; set; }
        public ICommand PopupAboutCefCommand { get; set; }

        private void DeleteCacheViaBat()
        {
            string bat = "taskkill /f /im CefFlashBrowser.exe\n" +
                         "timeout 1\n" +
                         "rd /s /q caches\\\n" +
                         "mshta vbscript:msgbox(\"done\",64,\"\")(window.close)\n" +
                         "start CefFlashBrowser.exe\n" +
                         "del _.bat";
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_.bat"), bat);

            Process.Start(new ProcessStartInfo()
            {
                FileName = "_.bat",
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private void DeleteCacheViaLauncher(string path)
        {
            Process.Start(new ProcessStartInfo()
            {
                FileName = path,
                Arguments = "-delcaches"
            });
        }

        private void DeleteCache()
        {
            if (JsConfirmDialog.Show(LanguageManager.GetString("message_deleteCache")))
            {
                var launcher = @"..\Launcher.exe";
                if (File.Exists(launcher))
                    DeleteCacheViaLauncher(launcher);
                else
                    DeleteCacheViaBat();
            }
        }

        private void PopupAboutCef()
        {
            BrowserWindow.Show("chrome://version/");
        }

        public BrowserSettingPanelViewModel()
        {
            DeleteCacheCommand = new DelegateCommand(DeleteCache);
            PopupAboutCefCommand = new DelegateCommand(PopupAboutCef);
        }
    }
}
