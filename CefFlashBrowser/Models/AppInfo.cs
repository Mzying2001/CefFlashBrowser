using System;
using System.Windows;

namespace CefFlashBrowser.Models
{
    public class AppInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string BaseDirectory { get; set; }

        public AppInfo()
        {
            Name = Application.ResourceAssembly.GetName().Name;
            Version = Application.ResourceAssembly.GetName().Version.ToString();
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
