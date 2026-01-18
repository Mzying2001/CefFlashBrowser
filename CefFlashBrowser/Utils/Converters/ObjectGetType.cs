using System;
using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class ObjectGetType : ValueConverterBase<object, Type>
    {
        public override Type Convert(object value, object parameter, CultureInfo culture)
        {
            return value?.GetType();
        }

        public override object ConvertBack(Type value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
