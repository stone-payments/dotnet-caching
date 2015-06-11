using System;
using System.Threading.Tasks;

namespace Vtex.Caching.Interfaces
{
    public interface ICache
    {
        Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync);
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? timeToLive);
        Task DeleteAsync(string key);
        Task<bool> ExistsAsync(string key);
        Task ExpireInAsync(string key, TimeSpan? timeToLive);
        Task<TimeSpan?> GetTimeToLiveAsync(string key);
    }
}
