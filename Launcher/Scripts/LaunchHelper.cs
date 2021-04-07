using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Launcher.Scripts
{
    class LaunchHelper
    {
        const string FolderName = "browser";

        const string ExeName = "CefFlashBrowser.exe";

        const string ProcName = "CefFlashBrowser";

        static readonly string CurrentPath = AppDomain.CurrentDomain.BaseDirectory;

        static readonly string TargetPath = Path.Combine(CurrentPath, FolderName);

        static readonly string TargetExePath = Path.Combine(TargetPath, ExeName);

        static readonly string CachesPath = Path.Combine(TargetPath, "caches");

        public static void KillProcs()
        {
            foreach (var proc in Process.GetProcessesByName(ProcName))
                proc.Kill();
        }

        public static void DelCaches()
        {
            bool flag;
            do
            {
                KillProcs();
                Thread.Sleep(1000);

                if (FolderRemover.TryRemove(CachesPath))
                {
                    flag = false;
                }
                else
                {
                    var dr = MessageBox.Show("An error occured, retry?", null, MessageBoxButton.YesNo);
                    flag = dr == MessageBoxResult.Yes;
                }
            } while (flag);

            //MessageBox.Show("done");
            Launch();
        }

        public static void Launch()
        {
            var info = new ProcessStartInfo()
            {
                FileName = TargetExePath,
                WorkingDirectory = FolderName
            };
            try
            {
                Process.Start(info);
            }
            catch
            {
                MessageBox.Show("ERROR");
            }
        }
    }
}
