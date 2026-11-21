using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CefFlashBrowser.Models.Downloads
{
    public enum DownloadState
    {
        Waiting,
        Downloading,
        Paused,
        Completed,
        Cancelled,
        Failed,
        TimedOut,
        Interrupted
    }

    public sealed class DownloadRecord
    {
        [JsonIgnore]
        public object SyncRoot { get; } = new object();

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Url { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string PartialPath { get; set; }
        public string Source { get; set; }
        public string SourcePage { get; set; }
        public string Method { get; set; } = "GET";
        public string ProtectedHeaders { get; set; }
        public bool Anonymous { get; set; }
        public bool Overwrite { get; set; }
        public bool IsLocalBody { get; set; }
        public string BodyPath { get; set; }
        public string ETag { get; set; }
        public string LastModified { get; set; }
        public bool Encoded { get; set; }
        public long Received { get; set; }
        public long? Total { get; set; }
        public DownloadState State { get; set; }
        public string Error { get; set; }
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Finished { get; set; }

        [JsonIgnore]
        public double Speed { get; set; }

        [JsonIgnore]
        public bool FileMissing { get; set; }

        [JsonIgnore]
        public IDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
