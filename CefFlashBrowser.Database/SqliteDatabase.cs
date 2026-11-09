using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Database
{
    public class SqliteDatabase : IDatabase
    {
        private readonly string _fileName;
        private readonly object _lock = new object();
        private SQLiteConnection _connection;
        private Task _work = Task.CompletedTask;
        private Task _stopTask;

        public SqliteDatabase(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("A database path is required.", nameof(fileName));

            _fileName = Path.GetFullPath(fileName);
        }

        private void EnsureConnection()
        {
            if (_connection != null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_fileName));

            var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = _fileName,
                Version = 3,
                Pooling = false,
                DefaultTimeout = 2
            }.ConnectionString);

            try
            {
                connection.Open();
                _connection = connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private Task<T> Enqueue<T>(Func<T> action)
        {
            lock (_lock)
            {
                if (_stopTask != null)
                    throw new ObjectDisposedException(nameof(SqliteDatabase));

                var task = _work.ContinueWith(previous =>
                {
                    // Observe failures without preventing later independent operations.
                    _ = previous.Exception;
                    EnsureConnection();
                    return action();
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

                _work = task;
                return task;
            }
        }

        private static IDictionary<string, object> CopyParameters(IDictionary<string, object> parameters)
        {
            return parameters == null ? null : new Dictionary<string, object>(parameters);
        }

        public Task<int> ExecuteAsync(string sql, IDictionary<string, object> parameters = null)
        {
            var values = CopyParameters(parameters);

            return Enqueue(() =>
            {
                using (var session = new Session(_connection))
                    return session.Execute(sql, values);
            });
        }

        public Task<object> ScalarAsync(string sql, IDictionary<string, object> parameters = null)
        {
            var values = CopyParameters(parameters);

            return Enqueue(() =>
            {
                using (var session = new Session(_connection))
                    return session.Scalar(sql, values);
            });
        }

        public Task<List<T>> QueryAsync<T>(string sql, Func<IDataRecord, T> map, IDictionary<string, object> parameters = null)
        {
            var values = CopyParameters(parameters);

            return Enqueue(() =>
            {
                using (var session = new Session(_connection))
                    return session.Query(sql, map, values);
            });
        }

        public Task TransactionAsync(Action<IDatabaseSession> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            return Enqueue(() =>
            {
                using (var transaction = _connection.BeginTransaction())
                using (var session = new Session(_connection, transaction))
                {
                    action(session);
                    transaction.Commit();
                }
                return true;
            });
        }

        public Task StopAsync()
        {
            lock (_lock)
            {
                return _stopTask ?? (_stopTask = _work.ContinueWith(previous =>
                {
                    _ = previous.Exception;
                    _connection?.Dispose();
                    _connection = null;
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default));
            }
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            GC.SuppressFinalize(this);
        }

        private sealed class Session : IDatabaseSession, IDisposable
        {
            private readonly SQLiteConnection _connection;
            private readonly SQLiteTransaction _transaction;
            private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
            private bool _disposed;

            public Session(SQLiteConnection connection, SQLiteTransaction transaction = null)
            {
                _connection = connection;
                _transaction = transaction;
            }

            private SQLiteCommand CreateCommand(string sql, IDictionary<string, object> parameters)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(Session));

                if (Thread.CurrentThread.ManagedThreadId != _threadId)
                    throw new InvalidOperationException("A database session cannot be used on another thread.");

                var command = new SQLiteCommand(sql, _connection, _transaction);
                try
                {
                    if (parameters != null)
                    {
                        foreach (var parameter in parameters)
                            command.Parameters.AddWithValue(parameter.Key, parameter.Value ?? DBNull.Value);
                    }
                    return command;
                }
                catch
                {
                    command.Dispose();
                    throw;
                }
            }

            public int Execute(string sql, IDictionary<string, object> parameters = null)
            {
                using (var command = CreateCommand(sql, parameters))
                    return command.ExecuteNonQuery();
            }

            public object Scalar(string sql, IDictionary<string, object> parameters = null)
            {
                using (var command = CreateCommand(sql, parameters))
                    return command.ExecuteScalar();
            }

            public List<T> Query<T>(string sql, Func<IDataRecord, T> map, IDictionary<string, object> parameters = null)
            {
                if (map == null)
                    throw new ArgumentNullException(nameof(map));

                using (var command = CreateCommand(sql, parameters))
                using (var reader = command.ExecuteReader())
                {
                    var results = new List<T>();
                    while (reader.Read())
                        results.Add(map(reader));
                    return results;
                }
            }

            public void Dispose()
            {
                _disposed = true;
            }
        }
    }
}
