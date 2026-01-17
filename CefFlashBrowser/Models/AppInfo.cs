using System.Reflection;
using System.Windows;

namespace CefFlashBrowser.Models
{
    public class AppInfo
    {
        public string Name { get; }
        public string Company { get; }
        public string Version { get; }

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
            Company = GetCompanyName(assembly);
            Version = assemblyName.Version.ToString();
        }

        private static string GetCompanyName(Assembly assembly)
        {
            string companyName = string.Empty;

            if (assembly.GetCustomAttribute<AssemblyCompanyAttribute>()
                is AssemblyCompanyAttribute companyAttribute)
            {
                companyName = companyAttribute.Company;
            }
            return companyName;
        }
    }
}
