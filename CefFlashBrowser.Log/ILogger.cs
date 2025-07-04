using System;
using System.Threading.Tasks;

namespace CefFlashBrowser.Log
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    public interface ILogger
    {
        void Log(LogLevel level, string message);
        void Log(LogLevel level, Exception exception);
        void Log(LogLevel level, string message, Exception exception);
        Task LogAsync(LogLevel level, string message);
        Task LogAsync(LogLevel level, Exception exception);
        Task LogAsync(LogLevel level, string message, Exception exception);
    }
}
