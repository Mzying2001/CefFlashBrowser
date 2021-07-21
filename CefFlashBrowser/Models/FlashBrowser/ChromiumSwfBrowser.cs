using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Models.FlashBrowser
{
    public class ChromiumSwfBrowser : FlashBrowserBase
    {


        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set
            {
                var url = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"html/FlashPlayer.html?src={value}");
                SetValue(FileNameProperty, url);
            }
        }

        // Using a DependencyProperty as the backing store for FileName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(ChromiumSwfBrowser), new PropertyMetadata(string.Empty));


    }
}
