using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CefFlashBrowser.Models;

namespace CefFlashBrowser.ViewModels
{
    class BrowserWindowViewModel : NotificationObject
    {
        private FlashBrowser _browser;

        public FlashBrowser Browser
        {
            get => _browser;
            set
            {
                _browser = value;
                RaisePropertyChanged("Browser");
            }
        }

        private string _title;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                RaisePropertyChanged("Title");
            }
        }

        public BrowserWindowViewModel(string url)
        {
            Title = url;

            Browser = new FlashBrowser(url);
            Browser.TitleChanged += (sender, e) =>
            {
                Title = e.NewValue.ToString();
            };
        }
    }
}
