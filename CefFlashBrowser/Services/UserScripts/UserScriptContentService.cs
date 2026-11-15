using CefFlashBrowser.Models;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Updates;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    public sealed class UserScriptContentService : IUserScriptDependencyProvider, IResourceProvider, IUserScriptUpdateFetcher, IDisposable
    {
        private readonly HttpClient _client;
        private readonly int _maximumBytes;
        private readonly TimeSpan _timeout;

        public UserScriptContentService(ProxySettings proxy = null, HttpMessageHandler handler = null,
            int maximumBytes = 10 * 1024 * 1024, TimeSpan? timeout = null)
        {
            if (maximumBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            _maximumBytes = maximumBytes;
            _timeout = timeout ?? TimeSpan.FromSeconds(30);
            if (handler == null)
            {
                var http = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                    UseDefaultCredentials = false,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                if (proxy?.EnableProxy == true)
                {
                    if (!int.TryParse(proxy.Port, out var port) || port <= 0 || port > 65535)
                        throw new ArgumentException("Invalid installation-content proxy port.", nameof(proxy));

                    var address = new UriBuilder("http", proxy.IP, port).Uri;
                    http.Proxy = new WebProxy(address)
                    {
                        Credentials = string.IsNullOrEmpty(proxy.UserName) ? null : new NetworkCredential(proxy.UserName, proxy.PlainPassword)
                    };
                }
                handler = http;
            }
            _client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }

        public Task<ResourceContent> GetScriptAsync(UserScriptInstallation installation, UserScriptDependencyDeclaration dependency, CancellationToken token)
            => FetchAsync(dependency.Url, true, false, token);

        public Task<ResourceContent> GetAsync(UserScriptInstallation installation, ResourceDeclaration resource, CancellationToken token)
            => FetchAsync(resource.Url, false, false, token);

        public async Task<string> FetchSourceAsync(UserScriptInstallation installation, string url, CancellationToken token)
            => (await FetchAsync(url, true, true, token).ConfigureAwait(false)).Text;

        public async Task<ResourceContent> FetchAsync(string url, bool decodeText, bool strictUtf8, CancellationToken token)
        {
            var uri = RequireHttp(url);
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(_timeout);
                for (var redirects = 0; ; redirects++)
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                    {
                        var status = (int)response.StatusCode;
                        if (status == 301 || status == 302 || status == 303 || status == 307 || status == 308)
                        {
                            if (redirects == 10 || response.Headers.Location == null)
                                throw new HttpRequestException("Invalid or excessive installation-content redirects.");
                            uri = RequireHttp(new Uri(uri, response.Headers.Location).AbsoluteUri);
                            continue;
                        }
                        response.EnsureSuccessStatusCode();
                        if (response.Content.Headers.ContentLength > _maximumBytes)
                            throw new IOException("Installation content exceeds its size limit.");
                        using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var output = new MemoryStream())
                        {
                            var buffer = new byte[65536];
                            int count;
                            while ((count = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false)) != 0)
                            {
                                if (output.Length + count > _maximumBytes)
                                    throw new IOException("Installation content exceeds its size limit.");
                                output.Write(buffer, 0, count);
                            }

                            var bytes = output.ToArray();
                            var charset = response.Content.Headers.ContentType?.CharSet?.Trim('"');
                            string text = null;
                            if (decodeText)
                            {
                                var encoding = strictUtf8 || string.IsNullOrWhiteSpace(charset)
                                    ? new UTF8Encoding(false, true)
                                    : Encoding.GetEncoding(charset, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                                text = encoding.GetString(bytes);
                                if (text.StartsWith("\uFEFF", StringComparison.Ordinal))
                                    text = text.Substring(1);
                            }

                            return new ResourceContent(bytes, response.Content.Headers.ContentType?.MediaType, text, charset);
                        }
                    }
                }
            }
        }

        private static Uri RequireHttp(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Installation content requires an HTTP(S) URL without embedded credentials.", nameof(url));

            return new UriBuilder(uri) { Fragment = string.Empty }.Uri;
        }

        public void Dispose() => _client.Dispose();
    }
}
