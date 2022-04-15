using System;
using System.Globalization;
using System.Linq;

namespace CefFlashBrowser.Utils.Converters
{
    public class UrlGetFileName : ValueConverterBase<string, string>
    {
        public override string Convert(string value, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return null;
            }
            else
            {
                return value.Split('/').LastOrDefault();
            }
        }

        public override string ConvertBack(string value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
