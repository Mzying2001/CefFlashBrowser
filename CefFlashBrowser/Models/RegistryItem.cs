using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models
{
    class RegistryItem : IDisposable, IEnumerable<KeyValuePair<string, object>>
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

        public RegistryItem SubKey(string name)
        {
            return new RegistryItem(_registryKey, name);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (string name in _registryKey.GetValueNames())
                yield return new KeyValuePair<string, object>(name, this[name]);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
