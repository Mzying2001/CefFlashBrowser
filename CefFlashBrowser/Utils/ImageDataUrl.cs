using System;
using System.Net.Http.Headers;

namespace CefFlashBrowser.Utils
{
    internal static class ImageDataUrl
    {
        private const string Prefix = "data:";

        internal static bool HasScheme(string url)
        {
            return url != null && url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryParse(string url, out string mediaType, out bool isBase64, out int payloadStart, out int payloadLength)
        {
            mediaType = null;
            isBase64 = false;
            payloadStart = 0;
            payloadLength = 0;

            if (!HasScheme(url))
                return false;

            int end = url.IndexOf('#', Prefix.Length);
            if (end < 0)
                end = url.Length;

            int separator = url.IndexOf(',', Prefix.Length, end - Prefix.Length);
            if (separator < 0 || separator + 1 == end)
                return false;

            var header = url.Substring(Prefix.Length, separator - Prefix.Length).Trim();
            int lastParameter = header.LastIndexOf(';');

            isBase64 = lastParameter >= 0
                && string.Equals(header.Substring(lastParameter + 1).TrimStart(' '), "base64", StringComparison.OrdinalIgnoreCase);

            if (isBase64)
                header = header.Substring(0, lastParameter).TrimEnd();

            if (!MediaTypeHeaderValue.TryParse(header, out var contentType)
                || !contentType.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return false;

            mediaType = contentType.MediaType;
            payloadStart = separator + 1;
            payloadLength = end - payloadStart;
            return true;
        }
    }
}
