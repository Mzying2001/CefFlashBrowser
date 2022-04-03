using System;
using System.Globalization;
using System.Windows;

namespace CefFlashBrowser.Utils.Converters
{
    public class VisiableIfNotZero : ValueConverterBase<int, Visibility>
    {
        public override Visibility Convert(int value, object parameter, CultureInfo culture)
        {
            return value == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public override int ConvertBack(Visibility value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
