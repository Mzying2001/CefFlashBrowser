using System;
using System.Collections.Generic;
using System.IO;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    internal sealed class UserScriptContentCache
    {
        private readonly string _objects, _temporary;
        private readonly Action<string> _report;

        internal UserScriptContentCache(string root, Action<string> report)
        {
            root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
            _objects = Path.Combine(root, "objects");
            _temporary = Path.Combine(root, "tmp");
            _report = report;
        }

        internal byte[] Read(string hash)
        {
            var path = ObjectPath(hash);

            UserScriptFiles.CheckPath(path);

            var bytes = UserScriptFiles.ReadBytes(path);

            if (UserScriptFiles.Hash(bytes) != hash)
                throw new IOException("Cached object hash mismatch: " + hash);

            return bytes;
        }

        internal string Write(byte[] bytes)
        {
            if (bytes.Length > UserScriptFiles.MaximumBytes)
                throw new IOException("Asset exceeds the 10 MiB limit.");

            var hash = UserScriptFiles.Hash(bytes);
            var path = ObjectPath(hash);

            UserScriptFiles.CheckPath(path);
            UserScriptFiles.CheckPath(_temporary);
            Directory.CreateDirectory(_objects);
            Directory.CreateDirectory(_temporary);

            if (UserScriptFiles.Exists(path))
            {
                try
                {
                    Read(hash);
                    return hash;
                }
                catch (IOException) { } // A freshly verified copy may replace a corrupt object of the same hash.
            }

            var temporary = Path.Combine(_temporary, Guid.NewGuid().ToString("N") + ".pending");

            try
            {
                UserScriptFiles.WriteNew(temporary, bytes);

                if (UserScriptFiles.Hash(UserScriptFiles.ReadBytes(temporary)) != hash)
                    throw new IOException("Object staging verification failed.");

                if (UserScriptFiles.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally { Cleanup(temporary); }

            return hash;
        }

        internal void Collect(ISet<string> referenced)
        {
            UserScriptFiles.CheckPath(_objects);

            if (Directory.Exists(_objects))
            {
                foreach (var path in Directory.EnumerateFiles(_objects))
                {
                    if (UserScriptFiles.IsHash(Path.GetFileName(path))
                        && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && !referenced.Contains(Path.GetFileName(path)))
                        Cleanup(path);
                }
            }

            UserScriptFiles.CheckPath(_temporary);

            if (Directory.Exists(_temporary))
            {
                foreach (var path in Directory.EnumerateFiles(_temporary, "*.pending"))
                {
                    if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out _)
                        && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
                        Cleanup(path);
                }
            }
        }

        private string ObjectPath(string hash)
        {
            if (!UserScriptFiles.IsHash(hash))
                throw new ArgumentException("Invalid content hash.", nameof(hash));

            return Path.Combine(_objects, hash);
        }

        private void Cleanup(string path)
        {
            try
            {
                if (UserScriptFiles.Exists(path))
                    File.Delete(path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                _report("Object cleanup pending: " + error.Message);
            }
        }
    }
}
