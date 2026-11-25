using SharpVectors.Converters;
using SharpVectors.Dom;
using SharpVectors.Renderers.Wpf;
using System;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

namespace CefFlashBrowser.Views.Custom
{
    internal static class BrowserIconImageLoader
    {
        internal static bool CanLoad(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps
                || (uri.IsFile && HasSvgExtension(uri));
        }

        internal static Task<ImageSource> LoadAsync(Uri uri, CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                if (uri.IsFile)
                {
                    using (var stream = File.OpenRead(uri.LocalPath))
                        return ReadSvg(stream);
                }

                return await ReadHttpImageAsync(uri, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }

        private static async Task<ImageSource> ReadHttpImageAsync(Uri uri, CancellationToken cancellationToken)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                var request = WebRequest.CreateHttp(uri);
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                try
                {
                    using (deadline.Token.Register(request.Abort))
                    using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
                    using (var stream = response.GetResponseStream())
                    {
                        if (HasSvgExtension(uri) || HasSvgExtension(response.ResponseUri) || IsSvgContentType(response.ContentType))
                            return ReadSvg(stream);

                        using (var buffer = new MemoryStream())
                        {
                            await stream.CopyToAsync(buffer, 81920, deadline.Token).ConfigureAwait(false);
                            buffer.Position = 0;
                            return ReadBitmap(buffer);
                        }
                    }
                }
                catch (WebException exception)
                {
                    exception.Response?.Dispose();
                    throw;
                }
            }
        }

        private static bool HasSvgExtension(Uri uri)
        {
            return uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSvgContentType(string contentType)
        {
            return MediaTypeHeaderValue.TryParse(contentType, out var parsed)
                && string.Equals(parsed.MediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase);
        }

        private static ImageSource ReadSvg(Stream stream)
        {
            var xmlSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = 10 * 1024 * 1024,
                XmlResolver = null
            };
            var drawingSettings = new WpfDrawingSettings
            {
                EnsureViewboxSize = true,
                ExternalResourcesAccessMode = ExternalResourcesAccessModes.Ignore,
                IncludeRuntime = false
            };

            using (var reader = XmlReader.Create(stream, xmlSettings))
            using (var svgReader = new FileSvgReader(drawingSettings))
            {
                if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "svg"
                    || reader.NamespaceURI != "http://www.w3.org/2000/svg")
                    return null;

                var drawing = svgReader.Read(reader);
                if (drawing == null)
                    return null;

                var image = new DrawingImage(drawing);
                image.Freeze();
                return image;
            }
        }

        private static ImageSource ReadBitmap(Stream stream)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
