using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using System;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class ThemeManager
    {
        public static Theme GetTheme()
        {
            return GlobalData.Settings.Theme;
        }

        public static void SetTheme(Theme theme)
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
            GlobalData.Settings.Theme = theme;
        }
    }
}
