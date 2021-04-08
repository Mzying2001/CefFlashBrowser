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
    static class LaunchHelper
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

        public static bool WaitKillProcs(double timeout = 5000)
        {
            var startTime = DateTime.Now;
            while (Process.GetProcessesByName(ProcName).Length != 0)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > timeout)
                    return false;
                Thread.Sleep(500);
            }
            return true;
        }

        public static void DelCaches()
        {
            bool loop = false;
            do
            {
                KillProcs();

                if (!WaitKillProcs())
                {
                    var mbr = MessageBox.Show("Kill process timed out, do you want to try again?",
                                              null, MessageBoxButton.YesNo);
                    if (mbr == MessageBoxResult.Yes)
                        continue;
                    else
                        break;
                }

                if (FolderRemover.TryRemove(CachesPath))
                {
                    loop = false;
                }
                else
                {
                    var mbr = MessageBox.Show("An error occurred. Do you want to try again?",
                                              null, MessageBoxButton.YesNo);
                    loop = mbr == MessageBoxResult.Yes;
                }

            } while (loop);

            //MessageBox.Show("done");
        }

        public static void Launch(string args)
        {
            var info = new ProcessStartInfo()
            {
                FileName = TargetExePath,
                WorkingDirectory = FolderName,
                Arguments = args
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
