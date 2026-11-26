using CefFlashBrowser.Database;
using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public partial class DatabaseService
    {
        private const string UpsertWebsiteIconSql =
            "INSERT INTO website_icons (page_url, icon_url, updated_at) " +
            "VALUES (@page, @icon, @now) " +
            "ON CONFLICT(page_url) DO UPDATE SET " +
            "icon_url = excluded.icon_url, " +
            "updated_at = excluded.updated_at " +
            "WHERE website_icons.icon_url <> excluded.icon_url";

        private static void CreateWebsiteIconsTable(IDatabaseSession session)
        {
            session.Execute(
                "CREATE TABLE IF NOT EXISTS website_icons ( " +
                "page_url TEXT PRIMARY KEY NOT NULL, " +
                "icon_url TEXT NOT NULL, " +
                "updated_at INTEGER NOT NULL)");
        }

        public async Task<List<WebsiteIconRecord>> GetWebsiteIconsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);

            return await _database.QueryAsync(
                "SELECT page_url, icon_url, updated_at FROM website_icons ORDER BY updated_at, page_url DESC",
                row => new WebsiteIconRecord(row.GetString(0), row.GetString(1), row.GetInt64(2))).ConfigureAwait(false);
        }

        public async Task SaveWebsiteIconAsync(WebsiteIconRecord icon)
        {
            if (icon == null)
                throw new ArgumentNullException(nameof(icon));

            await InitializeAsync().ConfigureAwait(false);
            await _database.ExecuteAsync(UpsertWebsiteIconSql, GetWebsiteIconParameters(icon)).ConfigureAwait(false);
        }

        internal async Task SaveWebsiteIconDiscoveryAsync(WebsiteIconRecord root, WebsiteIconRecord page, IReadOnlyList<string> keysToRemove)
        {
            if (page == null && keysToRemove.Count == 0)
            {
                await SaveWebsiteIconAsync(root).ConfigureAwait(false);
                return;
            }

            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                session.Execute(UpsertWebsiteIconSql, GetWebsiteIconParameters(root));

                if (page != null)
                {
                    session.Execute(UpsertWebsiteIconSql, GetWebsiteIconParameters(page));
                }
                foreach (string key in keysToRemove)
                {
                    session.Execute("DELETE FROM website_icons WHERE page_url = @page",
                        new Dictionary<string, object> { ["@page"] = key });
                }
            }).ConfigureAwait(false);
        }

        private static Dictionary<string, object> GetWebsiteIconParameters(WebsiteIconRecord icon)
        {
            return new Dictionary<string, object>
            {
                ["@page"] = icon.PageUrl,
                ["@icon"] = icon.IconUrl,
                ["@now"] = icon.UpdatedAt
            };
        }
    }
}
