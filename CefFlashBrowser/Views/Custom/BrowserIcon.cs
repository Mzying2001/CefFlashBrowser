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

        static BrowserIcon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BrowserIcon), new FrameworkPropertyMetadata(typeof(BrowserIcon)));
        }
    }
}
