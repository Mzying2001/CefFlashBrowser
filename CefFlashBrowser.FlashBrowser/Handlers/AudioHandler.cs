using CefSharp;
using CefSharp.Structs;
using System;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class AudioHandler : IAudioHandler
    {
        public virtual bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters)
        {
            return true;
        }

        public virtual void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage)
        {
        }

        public virtual void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
        {
        }

        public virtual void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels)
        {
        }

        public virtual void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
        }
    }
}
