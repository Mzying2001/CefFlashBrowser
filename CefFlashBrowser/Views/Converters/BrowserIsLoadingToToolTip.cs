using CefFlashBrowser.Models.StaticData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Views.Converters
{
    public class BrowserIsLoadingToToolTip : ValueConverterBase<bool, string>
    {
        public override string Convert(bool value, object parameter, CultureInfo culture)
        {
            return LanguageManager.GetString(value ? "toolTip_stop" : "toolTip_reload");
        }

        public override bool ConvertBack(string value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
