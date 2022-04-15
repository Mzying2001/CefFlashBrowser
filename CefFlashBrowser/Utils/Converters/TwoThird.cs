using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class TwoThird : ValueConverterBase<double, double>
    {
        public override double Convert(double value, object parameter, CultureInfo culture)
        {
            return value * 2d / 3d;
        }

        public override double ConvertBack(double value, object parameter, CultureInfo culture)
        {
            return value * 3d / 2d;
        }
    }
}
