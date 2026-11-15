using System;

namespace CefFlashBrowser.WinformCefSharp4WPF
{
    public sealed class NavigationFailedEventArgs : EventArgs
    {
        public string Address { get; }
        public Exception Exception { get; }

        public NavigationFailedEventArgs(string address, Exception exception)
        {
            Address = address;
            Exception = exception;
        }
    }
}
