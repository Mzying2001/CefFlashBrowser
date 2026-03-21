using System;
using System.IO;
using System.Net;

namespace CefFlashBrowser.Utils
{
    public static class UrlHelper
    {
        /// <summary>
        /// Determines whether the input string looks like a URL (similar to Chrome's address bar behavior).
        /// Returns true for URLs with explicit schemes, localhost, IP addresses, and domain-like strings.
        /// </summary>
        public static bool IsUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim();

            // 1. Has explicit scheme (http://, https://, ftp://, etc.)
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
                return uri.Scheme != Uri.UriSchemeFile;

            // 2. Try prepending http:// and check if it forms a valid URL
            if (Uri.TryCreate("http://" + input, UriKind.Absolute, out uri))
            {
                var host = uri.Host;

                // localhost (with optional port)
                if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    return true;

                // IP address
                if (IPAddress.TryParse(host, out _))
                    return true;

                // Domain-like: contains dot and is not a plain number (e.g. "3.14")
                if (host.Contains(".") && !double.TryParse(input, out _))
                    return true;
            }

            return false;
        }

        public static bool IsLocalSwfFile(string url)
        {
            return Path.GetExtension(url).Equals(".swf", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
        }

        public static bool IsLocalSolFile(string url)
        {
            return Path.GetExtension(url).Equals(".sol", StringComparison.OrdinalIgnoreCase) && File.Exists(url);
        }
    }
}
