using CefSharp;
using System;

namespace CefFlashBrowser.FlashBrowser
{
    public class JsContextEventArgs : EventArgs
    {
        public IBrowser Browser { get; }
        public IFrame Frame { get; }

        public JsContextEventArgs(IBrowser browser, IFrame frame)
        {
            Browser = browser;
            Frame = frame;
        }
    }
}
