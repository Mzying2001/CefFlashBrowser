using System;
using System.IO;
using System.Reflection;

public static class RegressionAssemblyResolver
{
    public static void Install(string directory)
    {
        var root = Path.GetFullPath(directory);

        AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
        {
            var name = new AssemblyName(args.Name).Name;

            if (name != Path.GetFileName(name))
                return null;

            foreach (var path in new[] { Path.Combine(root, name + ".dll"),
                Path.Combine(root, "Binaries", "CefSharp", name + ".dll") })
            {
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }

            return null;
        };
    }
}
