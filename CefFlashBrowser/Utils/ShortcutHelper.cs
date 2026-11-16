using System;
using System.Runtime.InteropServices;

namespace CefFlashBrowser.Utils
{
    internal static class ShortcutHelper
    {
        public static void Create(string fileName, string targetPath, string arguments)
        {
            object shellLinkObject = null;

            try
            {
                var shellLinkType = Type.GetTypeFromCLSID(Win32.ShellLinkClassId, true);
                shellLinkObject = Activator.CreateInstance(shellLinkType);

                var shellLink = (Win32.IShellLinkW)shellLinkObject;
                ThrowIfFailed(shellLink.SetPath(targetPath));
                ThrowIfFailed(shellLink.SetArguments(arguments ?? string.Empty));
                ThrowIfFailed(((Win32.IPersistFile)shellLink).Save(fileName, true));
            }
            finally
            {
                if (shellLinkObject != null && Marshal.IsComObject(shellLinkObject))
                    Marshal.FinalReleaseComObject(shellLinkObject);
            }
        }

        private static void ThrowIfFailed(int hResult)
        {
            if (hResult < 0)
                Marshal.ThrowExceptionForHR(hResult);
        }
    }
}
