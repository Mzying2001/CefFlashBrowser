using CefFlashBrowser.Log;
using CefFlashBrowser.Models.Data;
using SimpleMvvm.Ioc;
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
            await TryDeleteFilesAsync(GetDeleteFiles(logFiles), token).ConfigureAwait(false);
        }

        private static string[] GetDeleteFiles(string[] files)
        {
            int retainCount = Math.Max(GlobalData.Settings.RetainedLogCount, 0);

            if (files.Length <= retainCount)
                return Array.Empty<string>();

            return files.OrderBy(item => File.GetCreationTime(item)).Take(files.Length - retainCount).ToArray();
        }

        private static Task TryDeleteFilesAsync(IEnumerable<string> files, CancellationToken token)
        {
            return Task.Run(delegate
            {
                Parallel.ForEach(files, new ParallelOptions { CancellationToken = token }, item =>
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(item))
                        {
                            File.Delete(item);
                            LogInfo($"Deleted log file: {item}");
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"Failed to delete log file: {item}", e);
                    }
                });
            }, token);
        }

        private static ILogger GetLogger()
        {
            return SimpleIoc.Global.GetInstance<ILogger>();
        }

        public static void LogInfo(string message)
        {
            GetLogger().Log(LogLevel.Info, message);
        }

        public static void LogError(string message, Exception exception = null)
        {
            if (exception == null)
            {
                GetLogger().Log(LogLevel.Error, message);
            }
            else
            {
                GetLogger().Log(LogLevel.Error, message, exception);
            }
        }

        public static void LogWtf(string message, Exception exception = null) // What a Terrible Failure
        {
            try
            {
                if (exception == null)
                {
                    GetLogger().Log(LogLevel.Fatal, message);
                }
                else
                {
                    GetLogger().Log(LogLevel.Fatal, message, exception);
                }
            }
            catch { }
        }
    }
}
