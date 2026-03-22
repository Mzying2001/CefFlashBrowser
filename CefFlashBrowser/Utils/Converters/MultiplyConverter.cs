using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class MultiplyConverter : ValueConverterBase<double, double>
    {
        public double Multiplier { get; set; } = 1d;

        public override double Convert(double value, object parameter, CultureInfo culture)
        {
            return value * Multiplier;
        }

        public override double ConvertBack(double value, object parameter, CultureInfo culture)
        {
            return value / Multiplier;
        }
    }
}
