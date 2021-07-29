using CefFlashBrowser.Commands;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.Models
{
    class Website : NotificationObject
    {
        public ICommand OpenWebsiteCommand { get; private set; }

        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new Exception(LanguageManager.GetString("error_webSiteEmptyName"));
                else
                    _name = value;
                RaisePropertyChanged("Name");
            }
        }

        private string _url;

        public string Url
        {
            get => _url;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new Exception(LanguageManager.GetString("error_webSiteEmptyUrl"));
                else
                    _url = value;
                RaisePropertyChanged("Url");
            }
        }

        public Website(string name, string url)
        {
            Name = name;
            Url = url;

            OpenWebsiteCommand = new DelegateCommand(() =>
            {
                Views.BrowserWindow.Show(url);
            });
        }

        public static bool operator ==(Website left, Website right)
        {
            return left.Name == right.Name && left.Url == right.Url;
        }

        public static bool operator !=(Website left, Website right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
