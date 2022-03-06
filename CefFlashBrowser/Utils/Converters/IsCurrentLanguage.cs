using System;
using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class IsCurrentLanguage : ValueConverterBase<string, bool>
    {
        public override bool Convert(string value, object parameter, CultureInfo culture)
        {
            return value == LanguageManager.CurrentLanguage;
        }

        public override string ConvertBack(bool value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
