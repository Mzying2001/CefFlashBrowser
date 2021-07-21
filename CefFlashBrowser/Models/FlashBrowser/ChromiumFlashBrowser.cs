using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using CefFlashBrowser.Models.StaticData;
using CefSharp;
using CefSharp.Wpf;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class ChromiumFlashBrowser : FlashBrowserBase
    {
        public event EventHandler<NewWindowEventArgs> OnCreatedNewWindow;

        public override void BeginInit()
        {
            base.BeginInit();

            MenuHandler = new ContextMenuHandler();
            LifeSpanHandler = new FlashBrowserLifeSpanHandler((s, e) =>
            {
                OnCreatedNewWindow?.Invoke(s, e);
            });
        }
    }
}
