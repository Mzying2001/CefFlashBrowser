using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Log
{
    public class FileLogger : ILogger, IDisposable
    {
        private bool _disposed;
        private readonly string _newLine;
        private readonly FileStream _stream;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public FileLogger(string fileName)
        {
            CreateDirIfNotExists(fileName);

            _newLine = Environment.NewLine;
            _stream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _writer = new StreamWriter(_stream);
        }

        ~FileLogger()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _writer?.Dispose();
                    _stream?.Dispose();
                    _semaphore?.Dispose();
                }
                _disposed = true;
            }
        }

        private void CreateDirIfNotExists(string fileName)
        {
            string directory = Path.GetDirectoryName(fileName);

            if (!string.IsNullOrEmpty(directory)
                && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private string GetLogLevelString(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Debug: return "DEBUG  ";
                case LogLevel.Info: return "INFO   ";
                case LogLevel.Warning: return "WARNING";
                case LogLevel.Error: return "ERROR  ";
                case LogLevel.Fatal: return "FATAL  ";
                default: throw new ArgumentOutOfRangeException(nameof(level), level, null);
            }
        }

        private string GetTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        }

        private void WriteLine(string message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileLogger));

            _semaphore.Wait();

            try
            {
                _stream.Seek(0, SeekOrigin.End);
                _writer.WriteLine(message);
                _writer.Flush();
            }
            finally { _semaphore.Release(); }
        }

        private async Task WriteLineAsync(string message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileLogger));

            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                _stream.Seek(0, SeekOrigin.End);
                await _writer.WriteLineAsync(message).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            finally { _semaphore.Release(); }
        }

        public void Log(LogLevel level, string message)
        {
            WriteLine($"{GetTimeString()} | {GetLogLevelString(level)} | {message}");
        }

        public void Log(LogLevel level, Exception exception)
        {
            WriteLine($"{GetTimeString()} | {GetLogLevelString(level)} | {_newLine}{exception}");
        }

        public void Log(LogLevel level, string message, Exception exception)
        {
            WriteLine($"{GetTimeString()} | {GetLogLevelString(level)} | {message}{_newLine}{exception}");
        }

        public Task LogAsync(LogLevel level, string message)
        {
            return WriteLineAsync($"{GetTimeString()} | {GetLogLevelString(level)} | {message}");
        }

        public Task LogAsync(LogLevel level, Exception exception)
        {
            return WriteLineAsync($"{GetTimeString()} | {GetLogLevelString(level)} | {_newLine}{exception}");
        }

        public Task LogAsync(LogLevel level, string message, Exception exception)
        {
            return WriteLineAsync($"{GetTimeString()} | {GetLogLevelString(level)} | {message}{_newLine}{exception}");
        }
    }
}
