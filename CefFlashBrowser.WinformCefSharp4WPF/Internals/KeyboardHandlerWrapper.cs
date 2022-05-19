using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class KeyboardHandlerWrapper : IKeyboardHandler, IHandlerWrapper<IKeyboardHandler>
    {
        public IKeyboardHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public KeyboardHandlerWrapper(IKeyboardHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool OnKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey)
        {
            return Handler.OnKeyEvent(TargetBrowser, browser, type, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey);
        }

        public bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
        {
            return Handler.OnPreKeyEvent(TargetBrowser, browser, type, windowsKeyCode, nativeKeyCode, modifiers, isSystemKey, ref isKeyboardShortcut);
        }
    }
}
