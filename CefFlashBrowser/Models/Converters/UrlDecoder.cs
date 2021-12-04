using System.Globalization;
using System.Net;

namespace CefFlashBrowser.Models.Converters
{
    public class UrlDecoder : ValueConverterBase<string, string>
    {
        public override string Convert(string value, object parameter, CultureInfo culture)
        {
            return WebUtility.UrlDecode(value);
        }

        public override string ConvertBack(string value, object parameter, CultureInfo culture)
        {
            return WebUtility.UrlEncode(value);
        }
    }
}
