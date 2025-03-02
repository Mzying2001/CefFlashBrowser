using CefFlashBrowser.Models;
using System;
using System.Windows;

namespace CefFlashBrowser.Utils
{
    public static class ThemeManager
    {
        public static void SetTheme(Theme theme)
        {
            var dic = Application.Current.Resources.MergedDictionaries[1];

            switch (theme)
            {
                case Theme.Light:
                    dic.Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml");
                    break;

                case Theme.Dark:
                    dic.Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml");
                    break;
            }
        }
    }
}
