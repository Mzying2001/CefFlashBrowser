using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class NewPageEventArgs : EventArgs
    {
        public string Url { get; set; }

        public NewPageEventArgs(string url)
        {
            Url = url;
        }
    }
}
