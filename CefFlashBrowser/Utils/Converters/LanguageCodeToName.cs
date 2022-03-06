using System;
using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class LanguageCodeToName : ValueConverterBase<string, string>
    {
        public override string Convert(string value, object parameter, CultureInfo culture)
        {
            return LanguageManager.GetLanguageName(value);
        }

        public override string ConvertBack(string value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
