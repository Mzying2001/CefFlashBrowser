using CefSharp;
using System.Security.Cryptography.X509Certificates;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class RequestHandlerWrapper : IRequestHandler, IHandlerWrapper<IRequestHandler>
    {
        public IRequestHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public RequestHandlerWrapper(IRequestHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
        {
            return Handler.OnBeforeBrowse(TargetBrowser, browser, frame, request, userGesture, isRedirect);
        }

        public bool OnOpenUrlFromTab(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
        {
            return Handler.OnOpenUrlFromTab(TargetBrowser, browser, frame, targetUrl, targetDisposition, userGesture);
        }

        public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
        {
            return Handler.GetResourceRequestHandler(TargetBrowser, browser, frame, request, isNavigation, isDownload, requestInitiator, ref disableDefaultHandling);
        }

        public bool GetAuthCredentials(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, bool isProxy, string host, int port, string realm, string scheme, IAuthCallback callback)
        {
            return Handler.GetAuthCredentials(TargetBrowser, browser, originUrl, isProxy, host, port, realm, scheme, callback);
        }

        public bool OnQuotaRequest(IWebBrowser chromiumWebBrowser, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
        {
            return Handler.OnQuotaRequest(TargetBrowser, browser, originUrl, newSize, callback);
        }

        public bool OnCertificateError(IWebBrowser chromiumWebBrowser, IBrowser browser, CefErrorCode errorCode, string requestUrl, ISslInfo sslInfo, IRequestCallback callback)
        {
            return Handler.OnCertificateError(TargetBrowser, browser, errorCode, requestUrl, sslInfo, callback);
        }

        public bool OnSelectClientCertificate(IWebBrowser chromiumWebBrowser, IBrowser browser, bool isProxy, string host, int port, X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
        {
            return Handler.OnSelectClientCertificate(TargetBrowser, browser, isProxy, host, port, certificates, callback);
        }

        public void OnPluginCrashed(IWebBrowser chromiumWebBrowser, IBrowser browser, string pluginPath)
        {
            Handler.OnPluginCrashed(TargetBrowser, browser, pluginPath);
        }

        public void OnRenderViewReady(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnRenderViewReady(TargetBrowser, browser);
        }

        public void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser, CefTerminationStatus status)
        {
            Handler.OnRenderProcessTerminated(TargetBrowser, browser, status);
        }
    }
}
