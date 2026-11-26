using CefFlashBrowser.Database;
using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public partial class DatabaseService
    {
        private const string DownloadColumns =
            "id, url, file_name, file_path, partial_path, source, source_page, method, protected_headers, " +
            "anonymous, overwrite, is_local_body, body_path, etag, last_modified, encoded, received, total, " +
            "state, error, created_at, finished_at";

        private const string UpsertDownloadSql = "INSERT INTO downloads (" + DownloadColumns + ") VALUES (" +
            "@id, @url, @file_name, @file_path, @partial_path, @source, @source_page, @method, @protected_headers, " +
            "@anonymous, @overwrite, @is_local_body, @body_path, @etag, @last_modified, @encoded, @received, @total, " +
            "@state, @error, @created_at, @finished_at) " +
            "ON CONFLICT(id) DO UPDATE SET url = excluded.url, file_name = excluded.file_name, " +
            "file_path = excluded.file_path, partial_path = excluded.partial_path, source = excluded.source, " +
            "source_page = excluded.source_page, method = excluded.method, protected_headers = excluded.protected_headers, " +
            "anonymous = excluded.anonymous, overwrite = excluded.overwrite, is_local_body = excluded.is_local_body, " +
            "body_path = excluded.body_path, etag = excluded.etag, last_modified = excluded.last_modified, " +
            "encoded = excluded.encoded, received = excluded.received, total = excluded.total, state = excluded.state, " +
            "error = excluded.error, created_at = excluded.created_at, finished_at = excluded.finished_at";

        private static void CreateDownloadsTable(IDatabaseSession session)
        {
            session.Execute(
                "CREATE TABLE downloads ( " +
                "id TEXT PRIMARY KEY NOT NULL, " +
                "url TEXT NOT NULL, " +
                "file_name TEXT, " +
                "file_path TEXT, " +
                "partial_path TEXT, " +
                "source TEXT, " +
                "source_page TEXT, " +
                "method TEXT NOT NULL, " +
                "protected_headers TEXT, " +
                "anonymous INTEGER NOT NULL, " +
                "overwrite INTEGER NOT NULL, " +
                "is_local_body INTEGER NOT NULL, " +
                "body_path TEXT, " +
                "etag TEXT, " +
                "last_modified TEXT, " +
                "encoded INTEGER NOT NULL, " +
                "received INTEGER NOT NULL, " +
                "total INTEGER, " +
                "state INTEGER NOT NULL, " +
                "error TEXT, " +
                "created_at INTEGER NOT NULL, " +
                "finished_at INTEGER)");
        }

        internal async Task<List<DownloadRecord>> GetDownloadsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);

            var records = await _database.QueryAsync(
                "SELECT " + DownloadColumns + " FROM downloads ORDER BY created_at DESC, rowid", ReadDownload)
                .ConfigureAwait(false);

            foreach (var record in records)
            {
                if (record.State == DownloadState.Downloading || record.State == DownloadState.Waiting)
                    record.State = DownloadState.Interrupted;

                if (record.State != DownloadState.Completed && !string.IsNullOrEmpty(record.PartialPath))
                    record.Received = File.Exists(record.PartialPath) ? new FileInfo(record.PartialPath).Length : 0;
            }

            return records;
        }

        internal async Task ApplyDownloadChangesAsync(IReadOnlyList<DownloadRecord> upserts, IReadOnlyList<string> removedIds)
        {
            if (upserts.Count == 0 && removedIds.Count == 0)
                return;

            await InitializeAsync().ConfigureAwait(false);
            var snapshots = await Task.Run(() => upserts.Select(SnapshotDownload).ToArray()).ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                foreach (var id in removedIds)
                    session.Execute("DELETE FROM downloads WHERE id = @id", new Dictionary<string, object> { ["@id"] = id });
                foreach (var snapshot in snapshots)
                    session.Execute(UpsertDownloadSql, snapshot);
            }).ConfigureAwait(false);
        }

        private static Dictionary<string, object> SnapshotDownload(DownloadRecord record)
        {
            lock (record.SyncRoot)
            {
                return new Dictionary<string, object>
                {
                    ["@id"] = record.Id,
                    ["@url"] = record.Url,
                    ["@file_name"] = record.FileName,
                    ["@file_path"] = record.FilePath,
                    ["@partial_path"] = record.PartialPath,
                    ["@source"] = record.Source,
                    ["@source_page"] = record.SourcePage,
                    ["@method"] = record.Method,
                    ["@protected_headers"] = record.Headers.Count == 0 ? null
                        : PasswordProtector.Protect(JsonSerializer.Serialize(record.Headers)),
                    ["@anonymous"] = record.Anonymous ? 1 : 0,
                    ["@overwrite"] = record.Overwrite ? 1 : 0,
                    ["@is_local_body"] = record.IsLocalBody ? 1 : 0,
                    ["@body_path"] = record.BodyPath,
                    ["@etag"] = record.ETag,
                    ["@last_modified"] = record.LastModified,
                    ["@encoded"] = record.Encoded ? 1 : 0,
                    ["@received"] = record.Received,
                    ["@total"] = record.Total,
                    ["@state"] = (int)record.State,
                    ["@error"] = record.Error,
                    ["@created_at"] = record.Created.Ticks,
                    ["@finished_at"] = record.Finished?.Ticks
                };
            }
        }

        private static DownloadRecord ReadDownload(IDataRecord row)
        {
            var record = new DownloadRecord
            {
                Id = row.GetString(0),
                Url = row.GetString(1),
                FileName = row[2] as string,
                FilePath = row[3] as string,
                PartialPath = row[4] as string,
                Source = row[5] as string,
                SourcePage = row[6] as string,
                Method = row.GetString(7),
                Anonymous = row.GetInt64(9) != 0,
                Overwrite = row.GetInt64(10) != 0,
                IsLocalBody = row.GetInt64(11) != 0,
                BodyPath = row[12] as string,
                ETag = row[13] as string,
                LastModified = row[14] as string,
                Encoded = row.GetInt64(15) != 0,
                Received = row.GetInt64(16),
                Total = row.IsDBNull(17) ? (long?)null : row.GetInt64(17),
                State = (DownloadState)row.GetInt64(18),
                Error = row[19] as string,
                Created = new DateTime(row.GetInt64(20), DateTimeKind.Utc),
                Finished = row.IsDBNull(21) ? (DateTime?)null : new DateTime(row.GetInt64(21), DateTimeKind.Utc)
            };

            if (!Guid.TryParseExact(record.Id, "N", out _) || string.IsNullOrEmpty(record.Url))
                throw new InvalidDataException("Invalid download history record.");

            var headers = PasswordProtector.UnprotectOrEmpty(row[8] as string);

            if (!string.IsNullOrEmpty(headers))
            {
                var savedHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(headers)
                    ?? throw new JsonException("Download headers must be a JSON object.");

                foreach (var header in savedHeaders)
                    record.Headers[header.Key] = header.Value;
            }

            return record;
        }
    }
}
