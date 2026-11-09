using CefFlashBrowser.Database;
using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    // The application owns the injected database and closes it after its consumers stop.
    public class DatabaseService
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

                if (version > 1)
                    throw new InvalidOperationException("The browser database version is not supported.");

                if (version == 0)
                {
                    session.Execute(@"CREATE TABLE IF NOT EXISTS website_icons (
                        page_url TEXT PRIMARY KEY NOT NULL,
                        icon_url TEXT NOT NULL,
                        created_at INTEGER NOT NULL,
                        updated_at INTEGER NOT NULL,
                        deleted_at INTEGER NULL)");
                    session.Execute("PRAGMA user_version = 1");
                }
            }).ConfigureAwait(false);
        }

        public async Task<List<WebsiteIconRecord>> GetWebsiteIconsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);

            return await _database.QueryAsync(
                "SELECT page_url, icon_url, updated_at FROM website_icons WHERE deleted_at IS NULL ORDER BY updated_at, page_url DESC",
                row => new WebsiteIconRecord(row.GetString(0), row.GetString(1), row.GetInt64(2))).ConfigureAwait(false);
        }

        // Callers supply normalized URLs; URL matching and validation belong to the consumer.
        public async Task SaveWebsiteIconAsync(WebsiteIconRecord icon)
        {
            if (icon == null)
                throw new ArgumentNullException(nameof(icon));

            await InitializeAsync().ConfigureAwait(false);

            await _database.ExecuteAsync(@"INSERT INTO website_icons
                (page_url, icon_url, created_at, updated_at, deleted_at)
                VALUES (@page, @icon, @now, @now, NULL)
                ON CONFLICT(page_url) DO UPDATE SET
                    icon_url = excluded.icon_url,
                    updated_at = excluded.updated_at,
                    deleted_at = NULL
                WHERE website_icons.icon_url <> excluded.icon_url OR website_icons.deleted_at IS NOT NULL",
                new Dictionary<string, object>
                {
                    ["@page"] = icon.PageUrl,
                    ["@icon"] = icon.IconUrl,
                    ["@now"] = icon.UpdatedAt
                }).ConfigureAwait(false);
        }
    }
}
