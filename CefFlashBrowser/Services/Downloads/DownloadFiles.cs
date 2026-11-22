using CefFlashBrowser.Utils;
using System;
using System.IO;

namespace CefFlashBrowser.Services.Downloads
{
    public static class DownloadFiles
    {
        public static string DirectoryOrDefault(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
                    return UserScriptDownloadPaths.DefaultDirectory();

                var fullPath = Path.GetFullPath(directory);
                Directory.CreateDirectory(fullPath);
                return fullPath;
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException
                || error is IOException || error is UnauthorizedAccessException || error is System.Security.SecurityException)
            {
                return UserScriptDownloadPaths.DefaultDirectory();
            }
        }

        public static string AvailablePath(string path)
        {
            var directory = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);

            for (int suffix = 0; suffix < 10000; suffix++)
            {
                var candidate = suffix == 0 ? path : Path.Combine(directory, name + " (" + suffix + ")" + extension);

                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                    return candidate;
            }

            throw new IOException("No unused download file name was available.");
        }

        public static string Commit(string partial, string destination, bool overwrite)
        {
            if (overwrite)
            {
                if (File.Exists(destination))
                    File.Replace(partial, destination, null);
                else
                    File.Move(partial, destination);
                return destination;
            }

            for (int attempt = 0; attempt < 10000; attempt++)
            {
                var available = AvailablePath(destination);
                try
                {
                    File.Move(partial, available);
                    return available;
                }
                catch (IOException) when (File.Exists(available)) { }
            }

            throw new IOException("No unused download file name was available.");
        }

        public static void DeletePartial(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
    }
}
