using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.SettingPanelViewModels
{
    class BrowserSettingPanelViewModel : NotificationObject
    {
        public DelegateCommand DeleteCacheCommand { get; set; }

        private void DeleteCache()
        {
            var flag = MessageBox.Ask(LanguageManager.GetString("message_deleteCache"));

            if (flag == System.Windows.MessageBoxResult.OK)
            {
                string bat = $"taskkill /f /im CefFlashBrowser.exe\n" +
                             $"timeout 1\n" +
                             $"rd /s /q caches\\\n" +
                             $"start CefFlashBrowser.exe\n" +
                             $"del _.bat";
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_.bat"), bat);

                Process.Start(new ProcessStartInfo()
                {
                    FileName = "_.bat",
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
        }

        public BrowserSettingPanelViewModel()
        {
            DeleteCacheCommand = new DelegateCommand(p => DeleteCache());
        }
    }
}
