using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views.Custom
{
    public class BrowserIcon : Control
    {
        private Image _image;

        private static readonly DependencyPropertyKey HasImageFailedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HasImageFailed), typeof(bool), typeof(BrowserIcon), new PropertyMetadata(false));

        public static readonly DependencyProperty HasImageFailedProperty = HasImageFailedPropertyKey.DependencyProperty;

        public bool HasImageFailed
        {
            get { return (bool)GetValue(HasImageFailedProperty); }
        }

        public static readonly DependencyProperty FaviconUrlProperty =
            DependencyProperty.Register(nameof(FaviconUrl), typeof(string), typeof(BrowserIcon),
                new PropertyMetadata(null, (d, e) => ((BrowserIcon)d).SetValue(HasImageFailedPropertyKey, false)));

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

        public override void OnApplyTemplate()
        {
            if (_image != null)
                _image.ImageFailed -= ImageFailed;

            SetValue(HasImageFailedPropertyKey, false);
            base.OnApplyTemplate();
            _image = GetTemplateChild("favicon") as Image;
            if (_image != null)
                _image.ImageFailed += ImageFailed;
        }

        private void ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            // Image detaches the previous BitmapSource when Source changes. It clears
            // the failed source before raising ImageFailed; a new source stays visible.
            if (ReferenceEquals(sender, _image) && _image.Source == null)
                SetValue(HasImageFailedPropertyKey, true);
        }
    }
}
