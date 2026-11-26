using CefFlashBrowser.FlashBrowser;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CefFlashBrowser.Browser
{
    internal sealed class SwfPageContext
    {
        private static readonly string[] FlashAttributes =
        {
            "flashvars", "base", "wmode", "allowfullscreen", "allowfullscreeninteractive",
            "allownetworking", "bgcolor", "quality", "scale", "salign", "align", "menu", "play", "loop"
        };

        private const string InspectPage = @"(function (urls, names) {
            const base = document.baseURI;
            function urlKey(value) {
                if (!value) return null;
                try {
                    const url = new URL(value, base);
                    url.hash = '';
                    return url.href;
                } catch (_) { return null; }
            }
            const keys = urls.map(urlKey);
            const attributes = {};
            for (const element of document.querySelectorAll('embed[src], object')) {
                const params = new Map();
                for (const child of element.children) {
                    if (child.localName === 'param') {
                        const name = (child.getAttribute('name') || '').toLowerCase();
                        if (!params.has(name)) params.set(name, child.getAttribute('value'));
                    }
                }
                const source = element.localName === 'embed' ? element.getAttribute('src')
                    : element.getAttribute('data') || params.get('movie');
                const key = urlKey(source);
                if (key === null || !keys.includes(key)) continue;
                for (const name of names) {
                    const value = params.has(name) ? params.get(name) : element.getAttribute(name);
                    if (value != null) attributes[name] = value;
                }
                break;
            }
            return JSON.stringify({ base, origin: window.origin,
                ancestors: Array.from(location.ancestorOrigins || []), attributes });
        })";

        public SwfResourceRecord Resource { get; }
        public Uri FrameUrl { get; }
        public Uri BaseUrl { get; }
        public IReadOnlyList<Uri> Ancestors { get; }
        public IReadOnlyDictionary<string, string> Attributes { get; }

        private SwfPageContext(SwfResourceRecord resource, Uri frameUrl, Uri baseUrl,
            IReadOnlyList<Uri> ancestors, IReadOnlyDictionary<string, string> attributes)
        {
            Resource = resource;
            FrameUrl = frameUrl;
            BaseUrl = baseUrl;
            Ancestors = ancestors;
            Attributes = attributes;
        }

        public static async Task<SwfPageContext> TryReadAsync(ChromiumFlashBrowser browser, SwfResourceRecord resource)
        {
            if (!browser.IsSwfSourceCurrent(resource) || !Uri.TryCreate(resource.FrameUrl, UriKind.Absolute, out var source)
                || (!source.IsFile && (!IsWebUrl(source) || resource.HasOpaqueInitiator)))
                return null;

            try
            {
                using (var frame = browser.GetBrowser()?.GetFrame(resource.FrameId))
                {
                    if (!IsCurrentFrame(frame, source))
                        return null;

                    var urls = JsonSerializer.Serialize(new[] { resource.Url, resource.RequestedUrl });
                    var names = JsonSerializer.Serialize(FlashAttributes);
                    var response = await frame.EvaluateScriptAsync($"{InspectPage}({urls}, {names})",
                        timeout: TimeSpan.FromSeconds(2));

                    if (!response.Success || !(response.Result is string json) || !browser.IsSwfSourceCurrent(resource)
                        || !IsCurrentFrame(frame, source))
                        return null;

                    var values = JsonSerializer.Deserialize<PageValues>(json);
                    if (values == null || values.Ancestors == null)
                        return null;

                    var ancestors = new List<Uri>();
                    if (!source.IsFile)
                    {
                        if (!TryGetOrigin(values.Origin, out var origin) || origin != new Uri(source.GetLeftPart(UriPartial.Authority))
                            || frame.IsMain != (values.Ancestors.Length == 0))
                            return null;

                        if (!TryReadAncestors(frame, values.Ancestors, ancestors))
                            return null;
                    }

                    if (!Uri.TryCreate(values.Base, UriKind.Absolute, out var baseUrl)
                        || (!IsWebUrl(baseUrl) && !(source.IsFile && baseUrl.IsFile)))
                        baseUrl = source;

                    var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var name in FlashAttributes)
                    {
                        if (values.Attributes != null && values.Attributes.TryGetValue(name, out var value) && value != null)
                            attributes.Add(name, value);
                    }

                    return new SwfPageContext(resource, source, baseUrl, ancestors, attributes);
                }
            }
            catch (Exception e) when (e is InvalidOperationException || e is TimeoutException || e is JsonException)
            {
                // A closing or navigating frame may no longer have a usable JavaScript context.
                return null;
            }
        }

        private static bool IsCurrentFrame(IFrame frame, Uri source)
        {
            return frame?.IsValid == true && Uri.TryCreate(frame.Url, UriKind.Absolute, out var current) && current == source;
        }

        private static bool TryReadAncestors(IFrame frame, string[] origins, List<Uri> ancestors)
        {
            // CefSharp caches Parent wrappers and disposes them with the child wrapper.
            var parent = frame.Parent;

            foreach (var value in origins)
            {
                if (parent?.IsValid != true || !TryGetOrigin(value, out var origin)
                    || !Uri.TryCreate(parent.Url, UriKind.Absolute, out var parentUrl) || !IsWebUrl(parentUrl)
                    || origin != new Uri(parentUrl.GetLeftPart(UriPartial.Authority)))
                    return false;

                ancestors.Add(origin);
                parent = parent.Parent;
            }

            return parent == null;
        }

        private static bool TryGetOrigin(string value, out Uri origin)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out origin) && IsWebUrl(origin)
                && origin.UserInfo.Length == 0 && origin.PathAndQuery == "/" && origin.Fragment.Length == 0;
        }

        private static bool IsWebUrl(Uri url)
        {
            return url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;
        }

        private sealed class PageValues
        {
            [JsonPropertyName("base")]
            public string Base { get; set; }
            [JsonPropertyName("origin")]
            public string Origin { get; set; }
            [JsonPropertyName("ancestors")]
            public string[] Ancestors { get; set; }
            [JsonPropertyName("attributes")]
            public Dictionary<string, string> Attributes { get; set; }
        }
    }
}
