using CefFlashBrowser.Database;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    // Lock order: service lifecycle, storage queue, IDatabase queue. A database
    // transaction must never enter this queue or wait for UI work.
    public sealed class UserScriptStorage
    {
        private readonly DatabaseService _schema;
        private readonly object _lock = new object();
        private Task _work = Task.CompletedTask;
        private Task _stopTask;

        public IDatabase Database { get; }

        public UserScriptStorage(IDatabase database, DatabaseService schema)
        {
            Database = database ?? throw new ArgumentNullException(nameof(database));
            _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken token)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (_lock)
            {
                if (_stopTask != null)
                    throw new ObjectDisposedException(nameof(UserScriptStorage));

                var task = _work.ContinueWith(async previous =>
                {
                    _ = previous.Exception;
                    token.ThrowIfCancellationRequested();
                    await _schema.InitializeAsync().ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return await action().ConfigureAwait(false);
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
                _work = task;
                return task;
            }
        }

        public Task RunAsync(Func<Task> action, CancellationToken token)
        {
            return RunAsync(async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            }, token);
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                return _stopTask ?? (_stopTask = _work.ContinueWith(previous =>
                {
                    _ = previous.Exception;
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default));
            }
        }
    }
}
