using System;
using System.Threading.Tasks;

namespace StoneCo.Caching.Interfaces
{
    public interface IRawCache : ICache
    {
        Task RawSetAsync<T>(string key, T value, TimeSpan? timeToLive);
        Task<T> RawGetAsync<T>(string key);
    }
}
