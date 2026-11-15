using Mzying2001.MonkeySharp.Core.Apis;
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptClipboardService : IClipboardService
    {
        private readonly Dispatcher _dispatcher;
        private readonly CancellationToken _lifetime;

        public UserScriptClipboardService(Dispatcher dispatcher, CancellationToken lifetime = default(CancellationToken))
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _lifetime = lifetime;
        }

        public async Task SetTextAsync(string text, string mediaType, CancellationToken token)
        {
            var html = string.Equals(mediaType, "text/html", StringComparison.OrdinalIgnoreCase);
            if (!html && !string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Only text and HTML clipboard formats are supported.");

            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime))
            {
                for (var attempt = 0; ; attempt++)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    try
                    {
                        await _dispatcher.InvokeAsync(() =>
                        {
                            linked.Token.ThrowIfCancellationRequested();
                            var data = new DataObject();
                            data.SetText(text ?? string.Empty, TextDataFormat.UnicodeText);
                            if (html)
                                data.SetData(DataFormats.Html, BuildHtml(text ?? string.Empty));

                            Clipboard.SetDataObject(data, true);
                        });
                        return;
                    }
                    catch (ExternalException) when (attempt < 4)
                    {
                        await Task.Delay(60, linked.Token).ConfigureAwait(false);
                    }
                }
            }
        }

        internal static string BuildHtml(string fragment)
        {
            const string header = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            const string prefix = "<html><body><!--StartFragment-->";
            const string suffix = "<!--EndFragment--></body></html>";
            var htmlStart = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0));
            var fragmentStart = htmlStart + Encoding.UTF8.GetByteCount(prefix);
            var fragmentEnd = fragmentStart + Encoding.UTF8.GetByteCount(fragment);
            var htmlEnd = fragmentEnd + Encoding.UTF8.GetByteCount(suffix);

            return string.Format(CultureInfo.InvariantCulture, header, htmlStart, htmlEnd, fragmentStart, fragmentEnd)
                + prefix + fragment + suffix;
        }
    }
}
