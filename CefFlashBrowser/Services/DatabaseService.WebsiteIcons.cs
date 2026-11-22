using CefFlashBrowser.Database;
using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public partial class DatabaseService
    {
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

            var parameters = new Dictionary<string, object>
            {
                ["@page"] = icon.PageUrl,
                ["@icon"] = icon.IconUrl,
                ["@now"] = icon.UpdatedAt
            };

            await _database.ExecuteAsync(
                "INSERT INTO website_icons (page_url, icon_url, updated_at) " +
                "VALUES (@page, @icon, @now) " +
                "ON CONFLICT(page_url) DO UPDATE SET " +
                "icon_url = excluded.icon_url, " +
                "updated_at = excluded.updated_at " +
                "WHERE website_icons.icon_url <> excluded.icon_url", parameters).ConfigureAwait(false);
        }
    }
}
