using CefFlashBrowser.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels
{
    public class BrowserViewModel : ViewModelBase
    {
        private IWebBrowser _browser;
        public IWebBrowser Browser
        {
            get => _browser;
            set
            {
                if (_browser == null)
                {
                    UpdateValue(ref _browser, value);
                }
                else
                {
                    throw new InvalidOperationException(
                        "Browser property can only be set once.");
                }
            }
        }

        private string _address = "about:blank";
        public string Address
        {
            get => _address;
            set => UpdateValue(ref _address, value);
        }

        private string _title = string.Empty;
        public string Title
        {
            get => _title;
            set => UpdateValue(ref _title, value);
        }

        private IntPtr _integratedDevToolsHandle = IntPtr.Zero;
        public IntPtr IntegratedDevToolsHandle
        {
            get => _integratedDevToolsHandle;
            set => UpdateValue(ref _integratedDevToolsHandle, value);
        }

        private double _zoomLevel = 0.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (UpdateValue(ref _zoomLevel, value)
                    && GlobalData.Settings.SaveZoomLevel)
                {
                    GlobalData.Settings.BrowserZoomLevel = value;
                }
            }
        }

        public BrowserViewModel()
        {
            if (GlobalData.Settings.SaveZoomLevel)
            {
                ZoomLevel = GlobalData.Settings.BrowserZoomLevel;
            }
        }

        public void OnDevToolsOpened(IntPtr hDevTools)
        {
            if (GlobalData.Settings.EnableIntegratedDevTools)
            {
                HwndHelper.ApplyEmbeddedChildStyle(hDevTools);
                IntegratedDevToolsHandle = hDevTools;
            }
            Messenger.Global.Send(MessageTokens.DEVTOOLS_OPENED, Browser);
        }

        public void OnDevToolsClosed()
        {
            IntegratedDevToolsHandle = IntPtr.Zero;
            Messenger.Global.Send(MessageTokens.DEVTOOLS_CLOSED, Browser);
        }

        public void ToggleDevTools()
        {
            if (IntegratedDevToolsHandle == IntPtr.Zero
                && HwndHelper.FindNotIntegratedDevTools(Browser) == IntPtr.Zero)
            {
                Browser.ShowDevTools();
            }
            else
            {
                Browser.CloseDevTools();
            }
        }

        public void ExitFullScreen()
        {
            if (Browser.CanExecuteJavascriptInMainFrame)
            {
                Browser.ExecuteScriptAsync("if (document.fullscreenElement) document.exitFullscreen();");
            }
        }
    }
}
