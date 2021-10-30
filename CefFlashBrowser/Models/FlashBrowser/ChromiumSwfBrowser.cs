using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class ChromiumSwfBrowser : FlashBrowserBase
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
                if (d is ChromiumWebBrowser browser)
                {
                    browser.Address = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        $"Assets/Html/FlashPlayer.html?src={e.NewValue}");
                }
            }));


    }
}
