using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CefFlashBrowser.Services.Downloads
{
    public sealed class DownloadHistory
    {
        private readonly string _path;
        private readonly object _sync = new object();

        public DownloadHistory(string path) { _path = path; }

        public DownloadRecord[] Load()
        {
            if (!File.Exists(_path))
                return new DownloadRecord[0];

            var records = JsonConvert.DeserializeObject<DownloadRecord[]>(File.ReadAllText(_path, Encoding.UTF8))
                ?? new DownloadRecord[0];
            foreach (var record in records)
            {
                if (!Guid.TryParseExact(record.Id, "N", out _) || string.IsNullOrEmpty(record.Url))
                    throw new InvalidDataException("Invalid download history record.");

                var headers = PasswordProtector.UnprotectOrEmpty(record.ProtectedHeaders);
                if (!string.IsNullOrEmpty(headers))
                    record.Headers = JsonConvert.DeserializeObject<Dictionary<string, string>>(headers);

                if (record.State == DownloadState.Downloading || record.State == DownloadState.Waiting)
                    record.State = DownloadState.Interrupted;

                if (record.State != DownloadState.Completed && !string.IsNullOrEmpty(record.PartialPath))
                    record.Received = File.Exists(record.PartialPath) ? new FileInfo(record.PartialPath).Length : 0;

                record.FileMissing = record.State == DownloadState.Completed && !File.Exists(record.FilePath);
            }
            return records;
        }

        public void Save(IEnumerable<DownloadRecord> records)
        {
            lock (_sync)
            {
                var snapshots = records.Select(record =>
                {
                    lock (record.SyncRoot)
                    {
                        record.ProtectedHeaders = record.Headers.Count == 0 ? null
                            : PasswordProtector.Protect(JsonConvert.SerializeObject(record.Headers));
                        return JsonConvert.DeserializeObject<DownloadRecord>(JsonConvert.SerializeObject(record));
                    }
                }).ToArray();
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                FileHelper.SafeWriteFile(_path, JsonConvert.SerializeObject(snapshots, Formatting.Indented), Encoding.UTF8);
            }
        }
    }
}
