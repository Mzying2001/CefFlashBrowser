using CefFlashBrowser.FlashBrowser;
using CefSharp;
using CefSharp.Handler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CefFlashBrowser.Browser
{
    internal sealed class SwfPlayerPage
    {
        private readonly Dictionary<string, Document> _documents = new Dictionary<string, Document>(StringComparer.Ordinal);
        private readonly SwfResourceRecord _resource;
        private readonly string _moviePage;
        private readonly string _movieUrl;

        public string StartUrl { get; }

        public SwfPlayerPage(SwfPageContext context, string localPlayerUrl, string template, bool allowScriptAccess)
        {
            _resource = context.Resource;
            var playerUrl = context.FrameUrl.IsFile ? $"{localPlayerUrl}?context={Guid.NewGuid():N}" : context.FrameUrl.AbsoluteUri;
            _moviePage = GetRequestUrl(playerUrl);
            _movieUrl = GetRequestUrl(context.Resource.Url);

            var url = JsonSerializer.Serialize(context.Resource.Url);
            var baseUrl = JsonSerializer.Serialize(context.BaseUrl.AbsoluteUri);
            var attributes = JsonSerializer.Serialize(context.Attributes);
            var scriptAccess = JsonSerializer.Serialize(allowScriptAccess);
            var document = template.Replace("<!-- player initialization -->",
                $"<script>loadSwf({url}, {baseUrl}, {attributes}, {scriptAccess});</script>");
            var suppressReferrer = string.IsNullOrEmpty(_resource.ReferrerUrl) || _resource.ReferrerPolicy == ReferrerPolicy.NoReferrer;
            _documents.Add(_moviePage, new Document(document, context.Ancestors.Count == 0, suppressReferrer));

            var child = _moviePage;
            var title = WebUtility.HtmlEncode(context.Resource.FileName);

            // Keep the original chain of sites so Chromium can calculate the same cookie context.
            for (var i = 0; i < context.Ancestors.Count; i++)
            {
                var parent = new Uri(context.Ancestors[i], $"/.cef-flash-player/{Guid.NewGuid():N}").AbsoluteUri;
                var src = WebUtility.HtmlEncode(child);
                var html = $"<!doctype html><meta charset=utf-8><title>{title}</title>"
                    + $"<iframe src=\"{src}\" allowfullscreen style=\"position:fixed;inset:0;width:100%;height:100%;border:0\"></iframe>";
                _documents.Add(parent, new Document(html, i == context.Ancestors.Count - 1, suppressReferrer: true));
                child = parent;
            }

            StartUrl = child;
        }

        public bool ContainsDocument(string url) => _documents.ContainsKey(GetRequestUrl(url) ?? string.Empty);

        public IResourceRequestHandler GetHandler(IFrame frame, IRequest request, bool isNavigation, bool isDownload)
        {
            if (isDownload || frame == null || request.Method != "GET")
                return null;

            var url = GetRequestUrl(request.Url);
            if (isNavigation && url != null && _documents.TryGetValue(url, out var document)
                && frame.IsMain == document.IsMain)
                return document;

            if (!isNavigation && url == _movieUrl && GetRequestUrl(frame.Url) == _moviePage
                && Uri.TryCreate(url, UriKind.Absolute, out var target) && !target.IsFile)
                return new MovieRequest(_resource);

            return null;
        }

        private static string GetRequestUrl(string address)
        {
            return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Query) : null;
        }

        private sealed class Document : ResourceRequestHandler
        {
            private readonly byte[] _body;
            private readonly bool _suppressReferrer;
            public bool IsMain { get; }

            public Document(string html, bool isMain, bool suppressReferrer)
            {
                _body = Encoding.UTF8.GetBytes(html);
                _suppressReferrer = suppressReferrer;
                IsMain = isMain;
            }

            protected override IResourceHandler GetResourceHandler(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request)
            {
                var response = new ResourceHandler("text/html", new MemoryStream(_body, writable: false), true, "utf-8");
                response.Headers["Cache-Control"] = "no-store";

                if (_suppressReferrer)
                    response.Headers["Referrer-Policy"] = "no-referrer";
                return response;
            }
        }

        private sealed class MovieRequest : ResourceRequestHandler
        {
            private readonly SwfResourceRecord _resource;

            public MovieRequest(SwfResourceRecord resource) => _resource = resource;

            protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser control, IBrowser browser, IFrame frame,
                IRequest request, IRequestCallback callback)
            {
                request.SetReferrer(_resource.ReferrerUrl ?? string.Empty, _resource.ReferrerPolicy);
                return CefReturnValue.Continue;
            }
        }
    }
}
