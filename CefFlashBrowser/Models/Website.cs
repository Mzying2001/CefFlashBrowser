using System;

namespace CefFlashBrowser.Models
{
    public class Website : ICloneable
    {
        public string Name { get; set; }
        public string Url { get; set; }

        public Website()
        {
        }

        public Website(string name, string url)
        {
            Name = name;
            Url = url;
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        public Website Clone()
        {
            return new Website(Name, Url);
        }
    }
}
