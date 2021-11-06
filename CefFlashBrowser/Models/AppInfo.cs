using CefSharp;
using System;
using System.Windows;

namespace CefFlashBrowser.Models
{
    public class AppInfo
    {
        public string Name { get; }
        public string Version { get; }
        public string BaseDirectory { get; }

        public string CefSharpVersion { get; }

        public AppInfo()
        {
            Name = Application.ResourceAssembly.GetName().Name;
            Version = Application.ResourceAssembly.GetName().Version.ToString();
            BaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            CefSharpVersion = Cef.CefSharpVersion;
        }
    }
}
