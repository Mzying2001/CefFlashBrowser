using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal interface IHandlerWrapper<THandler>
    {
        THandler Handler { get; }
        IWebBrowser TargetBrowser { get; }
    }
}
