using System;
using System.Globalization;
using System.IO;

namespace CefFlashBrowser.Views.Converters
{
    public class UrlGetFileName : ValueConverterBase<string, string>
    {
        public override string Convert(string value, object parameter, CultureInfo culture)
        {
            return Path.GetFileName(value);
        }

        public override string ConvertBack(string value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
