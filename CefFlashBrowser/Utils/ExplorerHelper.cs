using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CefFlashBrowser.Utils
{
    internal static class ExplorerHelper
    {
        public static void SelectFile(string filePath)
        {
            Marshal.ThrowExceptionForHR(
                Win32.SHParseDisplayName(filePath, IntPtr.Zero, out IntPtr pidl, 0, out _));

            try
            {
                Marshal.ThrowExceptionForHR(Win32.SHOpenFolderAndSelectItems(pidl, 0, null, 0));
            }
            finally
            {
                Win32.ILFree(pidl);
            }
        }

        public static void OpenDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException($"Could not find the folder '{directoryPath}'.");

            var shellType = Type.GetTypeFromProgID("Shell.Application", true);
            var shell = Activator.CreateInstance(shellType);

            try
            {
                ((dynamic)shell).Explore(directoryPath);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }
}
