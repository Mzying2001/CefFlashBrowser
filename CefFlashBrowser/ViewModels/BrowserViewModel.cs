using CefFlashBrowser.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using SimpleMvvm;
using SimpleMvvm.Messaging;
using System;

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
                if (ReferenceEquals(_browser, value))
                    return;

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

        private string _address;
        public string Address
        {
            get => _address;
            set => UpdateValue(ref _address, value);
        }

        private string _title;
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



        public bool IsBrowserReady()
        {
            return Browser != null && !Browser.IsDisposed && Browser.IsBrowserInitialized;
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
            if (!IsBrowserReady())
                return;

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
            if (IsBrowserReady() && Browser.CanExecuteJavascriptInMainFrame)
            {
                Browser.ExecuteScriptAsync("if (document.fullscreenElement) document.exitFullscreen();");
            }
        }

        public void FindText(string searchText, bool forward, bool matchCase, bool findNext)
        {
            if (IsBrowserReady())
            {
                Browser.GetBrowser()?.Find(0, searchText, forward, matchCase, findNext);
            }
        }

        public void StopFinding(bool clearSelection)
        {
            if (IsBrowserReady())
            {
                Browser.GetBrowser()?.StopFinding(clearSelection);
            }
        }
    }
}
