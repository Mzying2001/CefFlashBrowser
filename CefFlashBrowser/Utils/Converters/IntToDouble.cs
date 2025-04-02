using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class IntToDouble : ValueConverterBase<int, double>
    {
        public override double Convert(int value, object parameter, CultureInfo culture)
        {
            return value;
        }

        public override int ConvertBack(double value, object parameter, CultureInfo culture)
        {
            return (int)value;
        }
    }
}
