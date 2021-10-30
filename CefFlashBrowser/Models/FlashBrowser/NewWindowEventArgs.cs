using System;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class NewWindowEventArgs : EventArgs
    {
        public string Url { get; set; }

        public bool CancelPopup { get; set; }

        public NewWindowEventArgs(string url, bool cancelPopup)
        {
            Url = url;
            CancelPopup = cancelPopup;
        }
    }
}
