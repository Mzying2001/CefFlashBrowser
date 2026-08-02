using System;
using System.IO;

namespace CefFlashBrowser.Utils
{
    public static class DirectoryAccessChecker
    {
        private const byte ProbeValue = 0xA5;

        public static bool CanReadAndWrite(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return false;

            string probePath = Path.Combine(
                directoryPath,
                ".CefFlashBrowser.AccessTest." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var stream = new FileStream(
                    probePath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.DeleteOnClose))
                {
                    stream.WriteByte(ProbeValue);
                    stream.Flush(true);
                    stream.Position = 0;
                    return stream.ReadByte() == ProbeValue;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
