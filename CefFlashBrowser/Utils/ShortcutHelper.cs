using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CefFlashBrowser.Utils
{
    internal static class ShortcutHelper
    {
        private static readonly Guid ShellLinkClassId = new Guid("00021401-0000-0000-C000-000000000046");

        public static void Create(string fileName, string targetPath, string arguments)
        {
            object shellLinkObject = null;

            try
            {
                var shellLinkType = Type.GetTypeFromCLSID(ShellLinkClassId, true);
                shellLinkObject = Activator.CreateInstance(shellLinkType);

                var shellLink = (IShellLinkW)shellLinkObject;
                ThrowIfFailed(shellLink.SetPath(targetPath));
                ThrowIfFailed(shellLink.SetArguments(arguments ?? string.Empty));
                ThrowIfFailed(((IPersistFile)shellLink).Save(fileName, true));
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

        [ComImport]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            [PreserveSig] int GetPath(StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            [PreserveSig] int GetIDList(out IntPtr ppidl);
            [PreserveSig] int SetIDList(IntPtr pidl);
            [PreserveSig] int GetDescription(StringBuilder pszName, int cch);
            [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            [PreserveSig] int GetWorkingDirectory(StringBuilder pszDir, int cch);
            [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            [PreserveSig] int GetArguments(StringBuilder pszArgs, int cch);
            [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            [PreserveSig] int GetHotkey(out short pwHotkey);
            [PreserveSig] int SetHotkey(short wHotkey);
            [PreserveSig] int GetShowCmd(out int piShowCmd);
            [PreserveSig] int SetShowCmd(int iShowCmd);
            [PreserveSig] int GetIconLocation(StringBuilder pszIconPath, int cch, out int piIcon);
            [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            [PreserveSig] int Resolve(IntPtr hwnd, uint fFlags);
            [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport]
        [Guid("0000010B-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            [PreserveSig] int GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
            [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            [PreserveSig] int GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }
    }
}
