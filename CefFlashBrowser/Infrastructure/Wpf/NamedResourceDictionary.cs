using System;
using System.Windows;

namespace CefFlashBrowser.Infrastructure.Wpf
{
    public class NamedResourceDictionary : ResourceDictionary
    {
        public string Name { get; set; }

        public NamedResourceDictionary()
        {
        }

        public NamedResourceDictionary(ResourceDictionary resourceDictionary)
        {
            if (resourceDictionary == null)
                throw new ArgumentNullException(nameof(resourceDictionary));

            foreach (var key in resourceDictionary.Keys)
            {
                this[key] = resourceDictionary[key];
            }
        }
    }
}
