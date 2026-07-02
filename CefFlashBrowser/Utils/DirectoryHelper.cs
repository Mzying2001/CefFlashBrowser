using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Utils
{
    public static class DirectoryHelper
    {
        public static void EnsureDirectoryExists(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        public static string GetParentDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                return null;

            dir = dir.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return Directory.GetParent(dir)?.FullName;
        }

        public static string[] GetSiblingDirectories(string dir)
        {
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            var parentDir = GetParentDirectory(dir);

            if (parentDir == null)
                return Array.Empty<string>();

            return Directory.GetDirectories(parentDir);
        }

        public static string GetDeleteMeDirectoryPath(string dir)
        {
            if (!Directory.Exists(dir))
                ThrowDirectoryNotFoundException(dir);

            var parentDir = GetParentDirectory(dir);

            if (parentDir == null)
                ThrowParentDirectoryNotFoundException(dir);

            var folderName = new DirectoryInfo(dir).Name;
            return Path.Combine(parentDir, $"{folderName}.{Guid.NewGuid()}.DELETEME");
        }

        public static void MoveDirectoryToPendingDelete(string dir, bool recreateEmpty = false)
        {
            if (!Directory.Exists(dir))
                ThrowDirectoryNotFoundException(dir);

            var deleteMePath = GetDeleteMeDirectoryPath(dir);
            Directory.Move(dir, deleteMePath);

            if (recreateEmpty)
            {
                Directory.CreateDirectory(dir);
            }
        }

        public static string[] GetPendingDeleteDirectories(string dir)
        {
            if (!Directory.Exists(dir))
                ThrowDirectoryNotFoundException(dir);

            var parentDir = GetParentDirectory(dir);

            if (parentDir == null)
                ThrowParentDirectoryNotFoundException(dir);

            var folderName = new DirectoryInfo(dir).Name;
            var deleteMePattern = $"{folderName}.*.DELETEME";

            return Directory.GetDirectories(parentDir, deleteMePattern);
        }

        private static void ThrowDirectoryNotFoundException(string dir)
        {
            throw new DirectoryNotFoundException($"Directory not found: {dir}");
        }

        private static void ThrowParentDirectoryNotFoundException(string dir)
        {
            throw new DirectoryNotFoundException($"The directory '{dir}' does not have a parent directory.");
        }

        public static Task DeletePendingDeleteDirectoriesAsync(string dir, CancellationToken token)
        {
            return Task.Run(() =>
            {
                foreach (var pendingDir in GetPendingDeleteDirectories(dir))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        Directory.Delete(pendingDir, recursive: true);
                        LogHelper.LogInfo($"Deleted directory: {pendingDir}");
                    }
                    catch (Exception e)
                    {
                        LogHelper.LogError($"Failed to delete directory: {pendingDir}", e);
                    }
                }
            }, token);
        }

        public static string GetRedirectDirectory(string baseDir, string folder, string fallbackDir = null)
        {
            string result = null;
            string desire = Path.Combine(baseDir, folder);

            if (string.IsNullOrWhiteSpace(fallbackDir))
            {
                fallbackDir = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
            }

            if (Directory.Exists(desire))
            {
                result = desire;
            }
            else
            {
                try
                {
                    string redirect = desire.TrimEnd(
                        Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (File.Exists(redirect))
                    {
                        result = File.ReadAllText(redirect);
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(result))
            {
                result = Path.Combine(fallbackDir, folder);
            }

            if (!result.EndsWith(Path.DirectorySeparatorChar.ToString()) &&
                !result.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                result += Path.DirectorySeparatorChar;
            }

            return result;
        }
    }
}
