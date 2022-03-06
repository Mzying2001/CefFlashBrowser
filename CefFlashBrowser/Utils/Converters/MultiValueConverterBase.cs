using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace CefFlashBrowser.Utils.Converters
{
    public abstract class MultiValueConverterBase<TIn, TOut> : IMultiValueConverter
    {
        object IMultiValueConverter.Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return Convert((from i in values select (TIn)i).ToArray(), parameter, culture);
        }

        object[] IMultiValueConverter.ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return (from i in ConvertBack((TOut)value, parameter, culture) select (object)i).ToArray();
        }

        public abstract TOut Convert(TIn[] values, object parameter, CultureInfo culture);
        public abstract TIn[] ConvertBack(TOut value, object parameter, CultureInfo culture);
    }
}
