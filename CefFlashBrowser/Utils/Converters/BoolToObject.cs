using System;
using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class BoolToObject : ValueConverterBase<bool, object>
    {
        public object TrueObject { get; set; }
        public object FalseObject { get; set; }

        public override object Convert(bool value, object parameter, CultureInfo culture)
        {
            return value ? TrueObject : FalseObject;
        }

        public override bool ConvertBack(object value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
