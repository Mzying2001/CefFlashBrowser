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

        public static readonly DependencyProperty FileNameProperty;

        private static void OnFileNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is IWebBrowser browser && e.NewValue is string fileName)
            {
                browser.Load(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"Assets/swfplayer.html?src={WebUtility.UrlEncode(fileName)}"));
            }
        }

        static ChromiumSwfBrowser()
        {
            FileNameProperty = DependencyProperty.Register("FileName", typeof(string), typeof(ChromiumSwfBrowser),
                                                            new PropertyMetadata(string.Empty, OnFileNameChanged));
        }



        public new string Address
        {
            get => base.Address;
        }

        public new void Load(string fileName)
        {
            FileName = fileName;
        }
    }
}
