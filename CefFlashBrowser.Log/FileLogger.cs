using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Log
{
    public class FileLogger : ILogger, IDisposable
    {
        private bool _disposed;
        private readonly FileStream _stream;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public FileLogger(string fileName)
        {
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

            try { _writer.WriteLine(message); }
            finally { _semaphore.Release(); }
        }

        private async Task WriteLineAsync(string message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileLogger));

            await _semaphore.WaitAsync().ConfigureAwait(false);

            try { await _writer.WriteLineAsync(message).ConfigureAwait(false); }
            finally { _semaphore.Release(); }
        }

        public void Log(LogLevel level, string message)
        {
            WriteLine($"{GetTimeString()} | {GetLogLevelString(level)} | {message}");
        }

        public void Log(LogLevel level, Exception exception)
        {
            WriteLine($"{GetTimeString()} | {GetLogLevelString(level)} | \n{exception}");
        }

        public void Log(LogLevel level, string message, Exception exception)
        {
            WriteLine($"{GetTimeString()} | {GetLogLevelString(level)} | {message}\n{exception}");
        }

        public async Task LogAsync(LogLevel level, string message)
        {
            await WriteLineAsync($"{GetTimeString()} | {GetLogLevelString(level)} | {message}");
        }

        public async Task LogAsync(LogLevel level, Exception exception)
        {
            await WriteLineAsync($"{GetTimeString()} | {GetLogLevelString(level)} | \n{exception}");
        }

        public async Task LogAsync(LogLevel level, string message, Exception exception)
        {
            await WriteLineAsync($"{GetTimeString()} | {GetLogLevelString(level)} | {message}\n{exception}");
        }
    }
}
