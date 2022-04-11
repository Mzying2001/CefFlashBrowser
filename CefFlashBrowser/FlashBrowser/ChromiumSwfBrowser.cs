using CefSharp;
using System;
using System.Net;
using System.Windows;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumSwfBrowser : ChromiumFlashBrowser
    {


        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FileName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(ChromiumSwfBrowser), new PropertyMetadata(string.Empty, (d, e) =>
            {
                if (d is IWebBrowser browser && e.NewValue is string fileName)
                {
                    browser.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        $"Assets/Html/FlashPlayer.html?src={WebUtility.UrlEncode(fileName)}"));
                }
            }));


    }
}
