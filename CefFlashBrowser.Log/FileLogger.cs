using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Log
{
    public class FileLogger : ILogger, IDisposable
    {
        private bool _disposed;
        private string _fileName;
        private FileStream _stream;
        private StreamWriter _writer;

        private readonly Func<DateTime> _getCurrentTime;
        private readonly Func<DateTime, string> _fileNameFactory;

        private readonly string _newLine = Environment.NewLine;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public FileLogger(string fileName)
            : this(_ => fileName)
        {
        }

        public FileLogger(Func<DateTime, string> fileNameFactory)
            : this(fileNameFactory, () => DateTime.Now)
        {
        }

        internal FileLogger(Func<DateTime, string> fileNameFactory, Func<DateTime> getCurrentTime)
        {
            _fileNameFactory = fileNameFactory
                ?? throw new ArgumentNullException(nameof(fileNameFactory));

            _getCurrentTime = getCurrentTime
                ?? throw new ArgumentNullException(nameof(getCurrentTime));

            EnsureWriter(_getCurrentTime());
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _semaphore.Wait();

            try
            {
                if (_disposed)
                    return;

                _disposed = true;
                _writer?.Dispose();
            }
            // Keep the semaphore available so queued writes can observe disposal.
            finally { _semaphore.Release(); }
        }

        private void EnsureWriter(DateTime time)
        {
            string fileName = _fileNameFactory(time);

            if (_writer != null && string.Equals(fileName, _fileName, StringComparison.OrdinalIgnoreCase))
                return;

            CreateDirIfNotExists(fileName);
            var stream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

            StreamWriter writer;

            try
            {
                writer = new StreamWriter(stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }

            var oldWriter = _writer;
            _stream = stream;
            _writer = writer;
            _fileName = fileName;
            oldWriter?.Dispose();
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

        private string PrepareWrite(LogLevel level, string message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileLogger));

            var time = _getCurrentTime();
            var line = $"{time:yyyy-MM-dd HH:mm:ss.fff} | {GetLogLevelString(level)} | {message}";

            EnsureWriter(time);
            return line;
        }

        private void WriteLine(LogLevel level, string message)
        {
            _semaphore.Wait();

            try
            {
                string line = PrepareWrite(level, message);
                _stream.Seek(0, SeekOrigin.End);
                _writer.WriteLine(line);
                _writer.Flush();
            }
            finally { _semaphore.Release(); }
        }

        private async Task WriteLineAsync(LogLevel level, string message)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                string line = PrepareWrite(level, message);
                _stream.Seek(0, SeekOrigin.End);
                await _writer.WriteLineAsync(line).ConfigureAwait(false);
                await _writer.FlushAsync().ConfigureAwait(false);
            }
            finally { _semaphore.Release(); }
        }

        public void Log(LogLevel level, string message)
        {
            WriteLine(level, message);
        }

        public void Log(LogLevel level, Exception exception)
        {
            WriteLine(level, $"{_newLine}{exception}");
        }

        public void Log(LogLevel level, string message, Exception exception)
        {
            WriteLine(level, $"{message}{_newLine}{exception}");
        }

        public Task LogAsync(LogLevel level, string message)
        {
            return WriteLineAsync(level, message);
        }

        public Task LogAsync(LogLevel level, Exception exception)
        {
            return WriteLineAsync(level, $"{_newLine}{exception}");
        }

        public Task LogAsync(LogLevel level, string message, Exception exception)
        {
            return WriteLineAsync(level, $"{message}{_newLine}{exception}");
        }
    }
}
