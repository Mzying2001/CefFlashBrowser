using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    internal static class UserScriptFiles
    {
        internal const int MaximumBytes = 10 * 1024 * 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        internal static string Decode(byte[] bytes)
        {
            var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
            return Utf8.GetString(bytes, offset, bytes.Length - offset);
        }

        internal static byte[] Encode(string source)
        {
            var bytes = Utf8.GetBytes(source);
            if (source.Length == 0 || source[0] != '\uFEFF') return bytes;
            var output = new byte[bytes.Length + 3];
            output[0] = 0xef; output[1] = 0xbb; output[2] = 0xbf;
            System.Buffer.BlockCopy(bytes, 0, output, 3, bytes.Length);
            return output;
        }

        internal static byte[] ReadBytes(string path)
        {
            using (var input = File.OpenRead(path))
            {
                if (input.Length > MaximumBytes) throw new IOException("Content exceeds the 10 MiB limit.");
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[65536];
                    int count;
                    while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (output.Length + count > MaximumBytes) throw new IOException("Content exceeds the 10 MiB limit.");
                        output.Write(buffer, 0, count);
                    }
                    return output.ToArray();
                }
            }
        }

        internal static string Hash(byte[] bytes)
        {
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static bool IsFileError(Exception error) => error is IOException || error is UnauthorizedAccessException;

        internal static bool IsHash(string value) => value != null && Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);

        internal static bool Exists(string path)
        {
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.Directory) != 0) throw new IOException("Expected a file: " + path);
                return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }

        internal static void CheckPath(string path)
        {
            var current = Path.GetFullPath(path);
            while (current != null)
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse points are not allowed in script storage: " + current);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                current = Path.GetDirectoryName(current);
            }
        }

        internal static void WriteNew(string path, byte[] bytes)
        {
            CheckPath(path);
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
        }
    }
}
