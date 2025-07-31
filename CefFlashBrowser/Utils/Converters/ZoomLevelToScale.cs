using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class ZoomLevelToScale : ValueConverterBase<double, double>
    {
        public override double Convert(double value, object parameter, CultureInfo culture)
        {
            return value >= 0 ? (value + 1) : (1 / (1 - value));
        }

        public override double ConvertBack(double value, object parameter, CultureInfo culture)
        {
            return value >= 1 ? (value - 1) : (1 - (1 / value));
        }
    }
}
