using System;
using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class ObjectToString : ValueConverterBase<object, string>
    {
        public override string Convert(object value, object parameter, CultureInfo culture)
        {
            return value?.ToString();
        }

        public override object ConvertBack(string value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
