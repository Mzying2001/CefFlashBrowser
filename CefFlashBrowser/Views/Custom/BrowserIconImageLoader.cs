using CefFlashBrowser.Utils;
using SharpVectors.Converters;
using SharpVectors.Dom;
using SharpVectors.Renderers.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;

namespace CefFlashBrowser.Views.Custom
{
    internal static class BrowserIconImageLoader
    {
        private static readonly Dictionary<string, Task<HttpIconResult>> HttpIconCache =
            new Dictionary<string, Task<HttpIconResult>>(StringComparer.Ordinal);

        internal static bool CanLoad(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps
                || (uri.IsFile && HasSvgExtension(uri));
        }

        internal static Task<ImageSource> LoadAsync(string url, Uri uri, CancellationToken cancellationToken)
        {
            if (uri != null && !uri.IsFile)
                return LoadHttpIconAsync(uri, cancellationToken);

            return Task.Run(() =>
            {
                if (uri == null)
                    return ReadDataUrl(url, cancellationToken);

                using (var stream = File.OpenRead(uri.LocalPath))
                    return ReadSvg(stream);
            }, cancellationToken);
        }

        private static async Task<ImageSource> LoadHttpIconAsync(Uri uri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string key = uri.GetLeftPart(UriPartial.Query);
            Task<HttpIconResult> load;

            lock (HttpIconCache)
            {
                if (!HttpIconCache.TryGetValue(key, out load)
                    || (load.Status == TaskStatus.RanToCompletion && load.Result.CanRetry))
                {
                    HttpIconCache[key] = load = Task.Run(() => DownloadHttpIconAsync(uri));
                }
            }

            // A control can stop waiting while the request continues for other controls.
            if (!load.IsCompleted && cancellationToken.CanBeCanceled)
            {
                var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => cancelled.TrySetCanceled()))
                    await Task.WhenAny(load, cancelled.Task).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return (await load.ConfigureAwait(false)).Source;
        }

        private static async Task<HttpIconResult> DownloadHttpIconAsync(Uri uri)
        {
            ImageSource source = null;
            try
            {
                source = await ReadHttpImageAsync(uri).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Share the fallback result when the request or decoder fails.
            }

            return new HttpIconResult(source);
        }

        private static ImageSource ReadDataUrl(string url, CancellationToken cancellationToken)
        {
            if (!ImageDataUrl.TryParse(url, out var mediaType, out bool isBase64, out int payloadStart, out int payloadLength))
                return null;

            cancellationToken.ThrowIfCancellationRequested();

            var bytes = Encoding.UTF8.GetBytes(url.Substring(payloadStart, payloadLength));
            int length = PercentDecode(bytes);

            if (isBase64)
            {
                bytes = DecodeBase64(bytes, length);
                length = bytes.Length;
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var stream = new MemoryStream(bytes, 0, length, writable: false))
            {
                return string.Equals(mediaType, "image/svg+xml", StringComparison.OrdinalIgnoreCase)
                    ? ReadSvg(stream) : ReadBitmap(stream);
            }
        }

        private static int PercentDecode(byte[] bytes)
        {
            int length = 0;

            for (int index = 0; index < bytes.Length; index++)
            {
                byte value = bytes[index];
                if (value == '%' && index + 2 < bytes.Length
                    && Uri.IsHexDigit((char)bytes[index + 1]) && Uri.IsHexDigit((char)bytes[index + 2]))
                {
                    value = (byte)(Uri.FromHex((char)bytes[index + 1]) * 16 + Uri.FromHex((char)bytes[index + 2]));
                    index += 2;
                }
                bytes[length++] = value;
            }
            return length;
        }

        private static byte[] DecodeBase64(byte[] bytes, int length)
        {
            int compactLength = 0;

            for (int index = 0; index < length; index++)
            {
                byte value = bytes[index];
                if (value != ' ' && value != '\t' && value != '\n' && value != '\r' && value != '\f')
                    bytes[compactLength++] = value;
            }

            var base64 = Encoding.ASCII.GetString(bytes, 0, compactLength);
            int remainder = compactLength % 4;
            if (remainder != 0)
            {
                // Only unpadded inputs can have two or three characters in the last group.
                if (remainder == 1 || base64.IndexOf('=') >= 0)
                    throw new FormatException("Invalid Base64 length or padding.");

                base64 = base64.PadRight(compactLength + 4 - remainder, '=');
            }
            return Convert.FromBase64String(base64);
        }

        private static async Task<ImageSource> ReadHttpImageAsync(Uri uri)
        {
            using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
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
                if (drawing == null || !HasDrawableContent(drawing))
                    return null;

                var image = new DrawingImage(drawing);
                image.Freeze();
                return image;
            }
        }

        private static bool HasDrawableContent(Drawing drawing)
        {
            if (drawing is DrawingGroup group)
            {
                if (group.Opacity <= 0)
                    return false;

                foreach (var child in group.Children)
                {
                    if (HasDrawableContent(child))
                        return true;
                }

                return false;
            }

            var bounds = drawing.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return false;

            // SharpVectors uses a transparent outline to preserve the viewBox, even for empty SVGs.
            if (drawing is GeometryDrawing geometry)
                return HasVisiblePaint(geometry.Brush)
                    || (geometry.Pen?.Thickness > 0 && HasVisiblePaint(geometry.Pen.Brush));

            if (drawing is GlyphRunDrawing text)
                return HasVisiblePaint(text.ForegroundBrush);

            if (drawing is ImageDrawing image)
                return image.ImageSource != null;

            return true;
        }

        private static bool HasVisiblePaint(Brush brush)
        {
            return brush != null && brush.Opacity > 0
                && (!(brush is SolidColorBrush solid) || solid.Color.A > 0);
        }

        private static ImageSource ReadBitmap(Stream stream)
        {
            if (HasIncompletePngChunks(stream))
                return null;

            var bitmap = new BitmapImage();
            bool decodeFailed = false;

            EventHandler<ExceptionEventArgs> onDecodeFailed = (sender, args) => decodeFailed = true;
            bitmap.DecodeFailed += onDecodeFailed;

            try
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
            }
            finally
            {
                bitmap.DecodeFailed -= onDecodeFailed;
            }

            if (decodeFailed)
                return null;

            bitmap.Freeze();
            return bitmap;
        }

        private static bool HasIncompletePngChunks(Stream stream)
        {
            const ulong pngSignature = 0x0A1A0A0D474E5089; // PNG signature read as little-endian.
            const uint endChunkType = 0x444E4549; // "IEND" read as little-endian.
            const int chunkOverhead = 12; // Length, type, and CRC.

            // WIC can accept truncated PNG data and supply transparent pixels without reporting failure.
            long start = stream.Position;
            try
            {
                using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
                {
                    if (stream.Length - start < sizeof(ulong) || reader.ReadUInt64() != pngSignature)
                        return false;

                    while (stream.Length - stream.Position >= chunkOverhead)
                    {
                        int length = IPAddress.NetworkToHostOrder(reader.ReadInt32());
                        uint type = reader.ReadUInt32();

                        if (length < 0 || length > stream.Length - stream.Position - sizeof(uint))
                            return true;

                        if (type == endChunkType)
                            return length != 0;

                        stream.Position += length + (long)sizeof(uint);
                    }

                    return true;
                }
            }
            finally
            {
                stream.Position = start;
            }
        }

        private sealed class HttpIconResult
        {
            private readonly long _retryAt;

            public ImageSource Source { get; }
            public bool CanRetry => Source == null && Stopwatch.GetTimestamp() >= _retryAt;

            public HttpIconResult(ImageSource source)
            {
                Source = source;

                if (source == null)
                {
                    _retryAt = Stopwatch.GetTimestamp() + 60 * Stopwatch.Frequency;
                }
            }
        }
    }
}
