using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using Microsoft.Win32;
using SimpleMvvm.Messaging;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CefFlashBrowser.Utils
{
    public static class ThemeManager
    {
        public static void ChangeTheme(Theme theme)
        {
            var skinDic = Application.Current.Resources.MergedDictionaries[1];
            var themeDic = Application.Current.Resources.MergedDictionaries[2];

            switch (theme)
            {
                case Theme.Light:
                    skinDic.Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml");
                    break;

                case Theme.Dark:
                    skinDic.Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml");
                    break;
            }

            themeDic.Source = themeDic.Source; // Refresh theme
            Messenger.Global.Send(MessageTokens.THEME_CHANGED, theme);
        }

        public static void ChangeTitleBarColor(Window window, Theme theme)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                int darkMode = theme == Theme.Dark ? 1 : 0;
                //Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                if (IsDarkModeSupported())
                {
                    int attribute = GetUseImmersiveDarkModeAttribute();
                    Win32.DwmSetWindowAttribute(hwnd, attribute, ref darkMode, sizeof(int));
                }
                else
                {
                    throw new NotSupportedException(
                        "Dark mode for title bars is not supported on this version of Windows.");
                }
            }
            catch (Exception e)
            {
                LogHelper.LogError("Error changing title bar color", e);
            }
        }

        public static bool IsSystemDarkMode()
        {
            const string registryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            const string registryValueName = "AppsUseLightTheme";

            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryKeyPath))
            {
                if (key != null)
                {
                    var value = key.GetValue(registryValueName);
                    if (value != null)
                        return value.ToString() == "0";
                }
            }
            return false;
        }

        public static Theme GetSystemTheme()
        {
            return IsSystemDarkMode() ? Theme.Dark : Theme.Light;
        }

        private static int GetWindowsBuildNumber()
        {
            var version = new Win32.OSVERSIONINFOW
            { dwOSVersionInfoSize = Marshal.SizeOf<Win32.OSVERSIONINFOW>() };

            // Environment.OSVersion may not return the correct
            // build number due to application manifest requirements,
            // so we use RtlGetVersion for accurate information.
            Win32.RtlGetVersion(ref version);
            return version.dwBuildNumber;
        }

        private static bool IsDarkModeSupported()
        {
            // Windows 10 1809 (Build 17763) started supporting dark mode for title bars
            return GetWindowsBuildNumber() >= 17763;
        }

        private static int GetUseImmersiveDarkModeAttribute()
        {
            // 1903 and later use 20 (DWMWA_USE_IMMERSIVE_DARK_MODE), older versions use 19
            return GetWindowsBuildNumber() >= 18362 ? 20 : 19;
        }
    }
}
