using SharpVectors.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Views.Converters
{
    public class BrowserIsLoadingToSvgViewBox : ValueConverterBase<bool, SvgViewbox>
    {
        private static SvgViewbox _reload, _stop;

        static BrowserIsLoadingToSvgViewBox()
        {
            _reload = new SvgViewbox()
            {
                Width = 14,
                Height = 14,
                Source = new Uri("svg/reload.svg", UriKind.Relative)
            };

            _stop = new SvgViewbox()
            {
                Width = 12,
                Height = 12,
                Source = new Uri("svg/stop.svg", UriKind.Relative)
            };
        }

        public override SvgViewbox Convert(bool value, object parameter, CultureInfo culture)
        {
            return value ? _stop : _reload;
        }

        public override bool ConvertBack(SvgViewbox value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
