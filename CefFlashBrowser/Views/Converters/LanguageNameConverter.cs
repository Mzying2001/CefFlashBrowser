using CefFlashBrowser.Services;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CefFlashBrowser.Views.Converters
{
    public class LanguageNameConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2
                && values[0] is string language
                && values[1] is LanguageService languageService)
            {
                return languageService.GetLanguageName(language) ?? language;
            }
            else
            {
                return DependencyProperty.UnsetValue;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
