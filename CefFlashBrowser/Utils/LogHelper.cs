using CefFlashBrowser.Models.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Utils
{
    public static class LogHelper
    {
        public static async Task DeleteExpiredLogsAsync(CancellationToken token)
        {
            var logFiles = Directory.GetFiles(GlobalData.LogsPath, "*.log");
            await TryDeleteFilesAsync(GetDeleteFiles(logFiles), token);
        }

        private static string[] GetDeleteFiles(string[] files)
        {
            int reserveDays = 14; // Number of days to keep logs

            if (files.Length <= reserveDays)
                return Array.Empty<string>();

            return files.OrderBy(item => File.GetCreationTime(item)).Take(files.Length - reserveDays).ToArray();
        }

        private static async Task TryDeleteFilesAsync(IEnumerable<string> files, CancellationToken token)
        {
            await Task.Run(delegate
            {
                Parallel.ForEach(files, new ParallelOptions { CancellationToken = token }, item =>
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(item))
                            File.Delete(item);
                    }
                    catch
                    { /*Ignore*/ }
                });
            }, token);
        }
    }
}
