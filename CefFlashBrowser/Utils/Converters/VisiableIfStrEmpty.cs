using System;
using System.Globalization;
using System.Windows;

namespace CefFlashBrowser.Utils.Converters
{
    public class VisiableIfStrEmpty : ValueConverterBase<string, Visibility>
    {
        public override Visibility Convert(string value, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value) ? Visibility.Visible : Visibility.Collapsed;
        }

        public override string ConvertBack(Visibility value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
