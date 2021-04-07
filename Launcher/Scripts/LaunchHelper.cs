using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher.Scripts
{
    class LaunchHelper
    {
        const string FolderName = "browser";

        const string ExeName = "CefFlashBrowser.exe";

        const string ProcName = "CefFlashBrowser";

        static readonly string CurrentPath = AppDomain.CurrentDomain.BaseDirectory;

        static readonly string TargetPath = Path.Combine(CurrentPath, FolderName);

        static readonly string TargetExe = Path.Combine(TargetPath, ExeName);

        public static void KillProcs()
        {
            foreach (var proc in Process.GetProcessesByName(ProcName))
                proc.Kill();
        }

        public static void DelCaches()
        {
            KillProcs();

            //delcaches...

            Launch();
        }

        public static void Launch()
        {
            var info = new ProcessStartInfo()
            {
                FileName = TargetExe,
                WorkingDirectory = FolderName
            };
            try
            {
                Process.Start(info);
            }
            catch
            {
                System.Windows.MessageBox.Show("ERROR");
            }
        }
    }
}
