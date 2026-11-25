using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Custom
{
    public class BrowserIcon : Control
    {
        public static readonly ComponentResourceKey TextFallbackStyleKey =
            new ComponentResourceKey(typeof(BrowserIcon), "TextFallbackStyle");

        private Image _image;
        private CancellationTokenSource _imageLoad;

        private static readonly DependencyPropertyKey HasImageFailedPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(HasImageFailed), typeof(bool), typeof(BrowserIcon), new PropertyMetadata(false));

        public static readonly DependencyProperty HasImageFailedProperty = HasImageFailedPropertyKey.DependencyProperty;

        public bool HasImageFailed
        {
            get { return (bool)GetValue(HasImageFailedProperty); }
        }

        public static readonly DependencyProperty FaviconUrlProperty =
            DependencyProperty.Register(nameof(FaviconUrl), typeof(string), typeof(BrowserIcon), new PropertyMetadata(null, (d, e) => ((BrowserIcon)d).UpdateImage()));

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

        public static readonly DependencyProperty DefaultIconTemplateProperty =
            DependencyProperty.Register(nameof(DefaultIconTemplate), typeof(DataTemplate), typeof(BrowserIcon), new PropertyMetadata(null));

        public DataTemplate DefaultIconTemplate
        {
            get { return (DataTemplate)GetValue(DefaultIconTemplateProperty); }
            set { SetValue(DefaultIconTemplateProperty, value); }
        }

        static BrowserIcon()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BrowserIcon), new FrameworkPropertyMetadata(typeof(BrowserIcon)));
        }

        public override void OnApplyTemplate()
        {
            if (_image != null)
                _image.ImageFailed -= ImageFailed;

            base.OnApplyTemplate();

            _image = GetTemplateChild("favicon") as Image;

            if (_image != null)
                _image.ImageFailed += ImageFailed;

            UpdateImage();
        }

        private void UpdateImage()
        {
            var previousLoad = _imageLoad;
            _imageLoad = null;
            previousLoad?.Cancel();

            SetValue(HasImageFailedPropertyKey, false);

            if (_image == null)
                return;

            _image.Source = null;
            var url = FaviconUrl;

            if (string.IsNullOrWhiteSpace(url))
                return;

            Uri uri = null;
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || (Uri.TryCreate(url, UriKind.Absolute, out uri) && BrowserIconImageLoader.CanLoad(uri)))
            {
                _imageLoad = new CancellationTokenSource();
                _ = LoadImageAsync(url, uri, _imageLoad);
            }
            else
            {
                _image.SetBinding(Image.SourceProperty, new Binding { Source = url });
            }
        }

        private async Task LoadImageAsync(string url, Uri uri, CancellationTokenSource load)
        {
            using (load)
            {
                ImageSource source = null;
                try
                {
                    source = await BrowserIconImageLoader.LoadAsync(url, uri, load.Token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Unavailable or unsupported images use the control's fallback.
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (!ReferenceEquals(_imageLoad, load))
                        return;

                    _imageLoad = null;
                    if (source == null)
                        SetValue(HasImageFailedPropertyKey, true);
                    else
                        _image.Source = source;
                }).Task.ConfigureAwait(false);
            }
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
