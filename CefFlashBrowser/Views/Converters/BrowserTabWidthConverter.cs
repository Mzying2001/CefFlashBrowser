using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CefFlashBrowser.Views.Converters
{
    public class BrowserTabWidthConverter : IMultiValueConverter
    {
        private const double DefaultWidth = 250;
        private const double MinimumWidth = 80;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 4
                || !(values[0] is double stripWidth) || stripWidth <= 0
                || !(values[1] is double buttonWidth) || buttonWidth <= 0
                || !(values[2] is Thickness buttonMargin)
                || !(values[3] is int tabCount) || tabCount <= 0)
                return DefaultWidth;

            var availableWidth = stripWidth - buttonWidth - buttonMargin.Left - buttonMargin.Right;
            return Math.Max(MinimumWidth, Math.Min(DefaultWidth, availableWidth / tabCount));
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
