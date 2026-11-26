using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts
{
    public static class UserScriptSourceText
    {
        public const int MaximumBytes = 10 * 1024 * 1024;

        public static void Validate(string source)
        {
            if (new UTF8Encoding(false, true).GetByteCount(source ?? string.Empty) > MaximumBytes)
                throw new IOException("Script source exceeds the 10 MiB size limit.");
        }

        public static async Task<string> ReadAsync(string path, CancellationToken token)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true))
            using (var output = new MemoryStream())
            {
                if (input.Length > MaximumBytes)
                    throw new IOException("Script source exceeds the 10 MiB size limit.");

                var buffer = new byte[65536];
                int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + count > MaximumBytes)
                        throw new IOException("Script source exceeds the 10 MiB size limit.");

                    output.Write(buffer, 0, count);
                }

                token.ThrowIfCancellationRequested();
                var source = new UTF8Encoding(false, true).GetString(output.ToArray());
                return source.StartsWith("\uFEFF", StringComparison.Ordinal) ? source.Substring(1) : source;
            }
        }

        public static async Task WriteAsync(string path, string source, CancellationToken token)
        {
            Validate(source);
            var bytes = new UTF8Encoding(false, true).GetBytes(source ?? string.Empty);
            token.ThrowIfCancellationRequested();

            using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
                await output.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        }
    }
}
