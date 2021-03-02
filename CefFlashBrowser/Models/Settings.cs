using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models
{
    static class Settings
    {
        private static RegistryKey _currentUser, _regKey;

        public static void Init()
        {
            _currentUser = Registry.CurrentUser;
            string keyName = @"SOFTWARE\CefFlashBrowser";

            if (!_currentUser.ContainsKey(keyName))
                _currentUser.CreateSubKey(keyName);

            _regKey = _currentUser.OpenSubKey(keyName, true);
        }

        public static void Close()
        {
            _currentUser.Close();
            _regKey.Close();
        }

        private static bool ContainsKey(this RegistryKey registryKey, string keyName)
        {
            return registryKey.GetSubKeyNames().Contains(keyName);
        }

        public static void WriteValue(string key, object value)
        {
            _regKey.SetValue(key, value);
        }

        public static string ReadValue(string key)
        {
            return _regKey.GetValue(key)?.ToString();
        }

        public static T ReadValue<T>(string key)
        {
            if (_regKey.ContainsKey(key))
                return (T)Convert.ChangeType(ReadValue(key), typeof(T));
            else
                return default;
        }

        public static string Language
        {
            get => ReadValue("language") ?? "en-us";
            set => WriteValue("language", value);
        }
    }
}
