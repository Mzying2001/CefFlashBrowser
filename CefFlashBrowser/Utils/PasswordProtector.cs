using System;
using System.Security.Cryptography;
using System.Text;

namespace CefFlashBrowser.Utils
{
    /// <summary>
    /// Protects stored passwords with the current Windows user's DPAPI credentials.
    /// </summary>
    public static class PasswordProtector
    {
        private const string Prefix = "dpapi:v1:";

        public static string Protect(string password)
        {
            if (string.IsNullOrEmpty(password))
                return string.Empty;

            byte[] data = Encoding.UTF8.GetBytes(password);

            try
            {
                byte[] protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Prefix + Convert.ToBase64String(protectedData);
            }
            finally
            {
                Array.Clear(data, 0, data.Length);
            }
        }

        public static string UnprotectOrEmpty(string password)
        {
            if (string.IsNullOrEmpty(password) || !password.StartsWith(Prefix, StringComparison.Ordinal))
                return string.Empty;

            byte[] data = null;

            try
            {
                byte[] protectedData = Convert.FromBase64String(password.Substring(Prefix.Length));
                data = ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (FormatException)
            {
                return string.Empty;
            }
            catch (CryptographicException)
            {
                return string.Empty;
            }
            finally
            {
                if (data != null)
                    Array.Clear(data, 0, data.Length);
            }
        }
    }
}
