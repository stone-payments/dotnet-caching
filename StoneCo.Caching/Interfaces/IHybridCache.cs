using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StoneCo.Caching.Interfaces
{
    public interface IHybridCache : IDisposable
    {
        Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync);
        Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<Dictionary<string, T>>> createManyAsync);
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? timeToLive);
        Task DeleteAsync(string key);
    }
}
