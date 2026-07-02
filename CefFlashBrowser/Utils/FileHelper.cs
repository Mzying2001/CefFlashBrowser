using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Utils
{
    public static class FileHelper
    {
        public static Task DeleteFilesAsync(IEnumerable<string> files, CancellationToken token)
        {
            return Task.Run(() =>
            {
                Parallel.ForEach(files, new ParallelOptions { CancellationToken = token }, item =>
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (File.Exists(item))
                        {
                            File.Delete(item);
                            LogHelper.LogInfo($"Deleted log file: {item}");
                        }
                    }
                    catch (Exception e)
                    {
                        LogHelper.LogError($"Failed to delete log file: {item}", e);
                    }
                });
            }, token);
        }

        public static void SafeWriteFile(string path, byte[] data)
        {
            var tmpPath = path + $".{Guid.NewGuid()}.tmp";

            try
            {
                File.WriteAllBytes(tmpPath, data);

                if (File.Exists(path))
                {
                    File.Replace(tmpPath, path, null);
                }
                else
                {
                    File.Move(tmpPath, path);
                }
            }
            catch (Exception e)
            {
                LogHelper.LogError($"Failed to write file: {path}", e);
                throw;
            }
            finally
            {
                if (File.Exists(tmpPath))
                {
                    try
                    {
                        File.Delete(tmpPath);
                    }
                    catch (Exception e)
                    {
                        LogHelper.LogError($"Failed to delete temp file: {tmpPath}", e);
                    }
                }
            }
        }

        public static void SafeWriteFile(string path, string contents)
        {
            SafeWriteFile(path, Encoding.UTF8.GetBytes(contents));
        }
    }
}
