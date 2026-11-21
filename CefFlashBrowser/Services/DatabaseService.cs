using CefFlashBrowser.Database;
using System;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    // The application owns the injected database and closes it after its consumers stop.
    public partial class DatabaseService
    {
        private readonly IDatabase _database;

        private readonly object _lock = new object();
        private Task _initializeTask;

        public DatabaseService(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task InitializeAsync()
        {
            lock (_lock)
            {
                // Share in-flight and successful initialization, but allow failures to retry.
                if (_initializeTask == null || _initializeTask.IsFaulted || _initializeTask.IsCanceled)
                    _initializeTask = InitializeCoreAsync();

                return _initializeTask;
            }
        }

        private async Task InitializeCoreAsync()
        {
            await _database.TransactionAsync(session =>
            {
                long version = Convert.ToInt64(session.Scalar("PRAGMA user_version"));

                if (version < 1)
                {
                    CreateWebsiteIconsTable(session);
                    session.Execute("PRAGMA user_version = 1");
                }

                if (version < 2)
                {
                    CreateUserScriptTables(session);
                    session.Execute("PRAGMA user_version = 2");
                }

                if (version < 3)
                {
                    AddUserScriptSortOrderColumn(session);
                    session.Execute("PRAGMA user_version = 3");
                }

                if (version < 4)
                {
                    CreateDownloadsTable(session);
                    session.Execute("PRAGMA user_version = 4");
                }
            }).ConfigureAwait(false);
        }
    }
}
