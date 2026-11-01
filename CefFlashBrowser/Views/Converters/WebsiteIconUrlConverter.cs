using CefFlashBrowser.Services;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CefFlashBrowser.Views.Converters
{
    public class WebsiteIconUrlConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            return values != null && values.Length >= 3
                && values[0] is string pageUrl && values[1] is IFaviconService service && values[2] is int
                ? service.GetIconUrl(pageUrl) : null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
