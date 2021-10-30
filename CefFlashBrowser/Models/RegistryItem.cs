using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CefFlashBrowser.Models
{
    public class RegistryItem : IDisposable, IEnumerable<(string name, object value)>
    {
        private RegistryKey _registryKey;

        public RegistryItem(RegistryKey parent, string itemName)
        {
            if (!parent.GetSubKeyNames().Contains(itemName))
                parent.CreateSubKey(itemName);
            _registryKey = parent.OpenSubKey(itemName, true);
        }

        public void Dispose()
        {
            _registryKey.Close();
            _registryKey.Dispose();
        }

        public object this[string name]
        {
            get => _registryKey.GetValue(name);
            set
            {
                if (value == null)
                    _registryKey.DeleteValue(name);
                else
                    _registryKey.SetValue(name, value);
            }
        }

        public T GetValue<T>(string name)
        {
            var value = this[name];
            return value == null ? default : (T)Convert.ChangeType(value, typeof(T));
        }

        public RegistryItem SubItem(string name)
        {
            return new RegistryItem(_registryKey, name);
        }

        public IEnumerator<(string name, object value)> GetEnumerator()
        {
            foreach (string name in _registryKey.GetValueNames())
                yield return (name, this[name]);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
