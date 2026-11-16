using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CefFlashBrowser.Utils
{
    public static class UserScriptDownloadPaths
    {
        public static string DefaultDirectory()
        {
            var id = new Guid("374DE290-123F-4565-9164-39C4925E467B");
            IntPtr path = IntPtr.Zero;

            try
            {
                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out path) == 0 && path != IntPtr.Zero)
                    return Marshal.PtrToStringUni(path);
            }
            finally
            {
                if (path != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(path);
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public static string SafeFileName(string suggested)
        {
            var name = (suggested ?? string.Empty).Replace('\\', '/');
            name = name.Substring(name.LastIndexOf('/') + 1);

            var invalid = Path.GetInvalidFileNameChars();
            name = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim().TrimEnd('.');

            if (name.Length > 150)
                name = name.Substring(0, 150).TrimEnd('.');

            var stem = name.Split('.')[0].ToUpperInvariant();

            if (new[] { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" }.Contains(stem)
                || Enumerable.Range(1, 9).Any(index => stem == "COM" + index || stem == "LPT" + index))
                name = "_" + name;

            if (name.Length > 150)
                name = name.Substring(0, 150).TrimEnd('.');

            return string.IsNullOrEmpty(name) ? "download.bin" : name;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    }
}
