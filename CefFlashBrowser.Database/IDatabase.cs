using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace CefFlashBrowser.Database
{
    public interface IDatabase : IDisposable
    {
        Task<int> ExecuteAsync(string sql, IDictionary<string, object> parameters = null);
        Task<object> ScalarAsync(string sql, IDictionary<string, object> parameters = null);
        Task<List<T>> QueryAsync<T>(string sql, Func<IDataRecord, T> map, IDictionary<string, object> parameters = null);
        Task TransactionAsync(Action<IDatabaseSession> action);
        Task StopAsync();
    }

    // A session is valid only for the duration of its transaction callback.
    // Callbacks and row mappers must not queue or wait for another database operation.
    public interface IDatabaseSession
    {
        int Execute(string sql, IDictionary<string, object> parameters = null);
        object Scalar(string sql, IDictionary<string, object> parameters = null);
        List<T> Query<T>(string sql, Func<IDataRecord, T> map, IDictionary<string, object> parameters = null);
    }
}
