using CefSharp;
using System;
using System.Threading.Tasks;

public static class UserScriptFrameProbe
{
    public static Task<JavascriptResponse> EvaluateAsync(IWebBrowser browser, string url, string code)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            var native = browser.GetBrowser();

            foreach (var id in native.GetFrameIdentifiers())
            {
                using (var frame = native.GetFrame(id))
                {
                    if (frame != null && frame.IsValid && frame.Url == url)
                        return frame.EvaluateScriptAsync(code);
                }
            }

            throw new InvalidOperationException("Fixture frame not found: " + url);
        }).Unwrap();
    }
}
