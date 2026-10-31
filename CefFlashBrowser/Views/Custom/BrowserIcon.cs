using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views.Custom
{
    public class BrowserIcon : Control
    {
        public static readonly DependencyProperty FaviconUrlProperty =
            DependencyProperty.Register(nameof(FaviconUrl), typeof(string), typeof(BrowserIcon), new PropertyMetadata(null));

        public string FaviconUrl
        {
            get { return (string)GetValue(FaviconUrlProperty); }
            set { SetValue(FaviconUrlProperty, value); }
        }

        public static readonly DependencyProperty DefaultIconProperty =
            DependencyProperty.Register(nameof(DefaultIcon), typeof(object), typeof(BrowserIcon), new PropertyMetadata(null));

        public object DefaultIcon
        {
            get { return GetValue(DefaultIconProperty); }
            set { SetValue(DefaultIconProperty, value); }
        }

        static BrowserIcon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BrowserIcon), new FrameworkPropertyMetadata(typeof(BrowserIcon)));
        }
    }
}
