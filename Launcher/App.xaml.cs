using Launcher.Scripts;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Launcher
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var args = new List<string>();
            foreach (var item in e.Args)
            {
                if (item == "-delcaches")
                    LaunchHelper.DelCaches();
                else
                    args.Add(item);
            }

            LaunchHelper.Launch(GetArgsStr(args));
            Environment.Exit(0);
        }

        private string GetArgsStr(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (var item in args)
                sb.Append(item).Append(" ");
            return sb.ToString();
        }
    }
}
