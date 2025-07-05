using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using Microsoft.Win32;
using SimpleMvvm.Messaging;
using System;
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
                Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
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
    }
}
