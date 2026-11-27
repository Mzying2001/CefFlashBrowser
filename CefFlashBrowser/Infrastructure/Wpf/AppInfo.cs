using System.Reflection;
using System.Windows;

namespace CefFlashBrowser.Infrastructure.Wpf
{
    public class AppInfo
    {
        public string Name { get; }
        public string Company { get; }
        public string Version { get; }
        public string Copyright { get; }

        public string DisplayVersion
        {
#if DEBUG
            get => $"version {Version} (Debug) | by {Company}";
#else
            get => $"version {Version} | by {Company}";
#endif
        }

        public AppInfo()
        {
            var assembly = Application.ResourceAssembly;
            var assemblyName = assembly.GetName();

            Name = assemblyName.Name;
            Company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? string.Empty;
            Version = assemblyName.Version.ToString();
            Copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? string.Empty;
        }
    }
}
