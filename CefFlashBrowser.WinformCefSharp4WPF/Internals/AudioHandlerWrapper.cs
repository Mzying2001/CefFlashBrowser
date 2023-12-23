using CefSharp;
using CefSharp.Structs;
using System;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class AudioHandlerWrapper : IAudioHandler, IHandlerWrapper<IAudioHandler>
    {
        public IAudioHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public AudioHandlerWrapper(IAudioHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters)
        {
            return Handler.GetAudioParameters(TargetBrowser, browser, ref parameters);
        }

        public void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage)
        {
            Handler.OnAudioStreamError(TargetBrowser, browser, errorMessage);
        }

        public void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
        {
            Handler.OnAudioStreamPacket(TargetBrowser, browser, data, noOfFrames, pts);
        }

        public void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels)
        {
            Handler.OnAudioStreamStarted(TargetBrowser, browser, parameters, channels);
        }

        public void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            Handler.OnAudioStreamStopped(TargetBrowser, browser);
        }
    }
}
