using System;
using System.Globalization;
using System.Linq;

namespace CefFlashBrowser.Utils.Converters
{
    public class StringArrNotAnyEmpty : MultiValueConverterBase<string, bool>
    {
        public override bool Convert(string[] values, object parameter, CultureInfo culture)
        {
            return values.All(i => !string.IsNullOrWhiteSpace(i));
        }

        public override string[] ConvertBack(bool value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
