using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using StoneCo.Caching.Interfaces;

namespace StoneCo.Caching.Backends.InProcess
{
    public class InProcessCache : IRawCache
    {
        private readonly MemoryCache _cache;

        public InProcessCache()
        {
            var options = new MemoryCacheOptions
            {
                
            };
            _cache = new MemoryCache(options);
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync)
        {
            var item = _cache.GetOrCreateAsync(key, )GetCacheItem(key);
            if (item != null)
                return ((InProcessCacheWrapper<T>)item.Value).Value;

            var value = await createAsync().ConfigureAwait(false);
            await SetAsync(key, value, timeToLive).ConfigureAwait(false);
            return value;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var item = await RawGetAsync<InProcessCacheWrapper<T>>(key).ConfigureAwait(false);
            return item != null ? item.Value : default(T);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            await RawSetAsync(key, InProcessCacheWrapper<T>.For(value, timeToLive), timeToLive).ConfigureAwait(false);
        }

        public Task DeleteAsync(string key)
        {
            _cache.Remove(key);

            return Task.FromResult(0);
        }

        public async Task ExpireInAsync(string key, TimeSpan? timeToLive)
        {
            var item = await RawGetAsync<object>(key).ConfigureAwait(false) as IInProcessCacheWrapper;

            if (item != null)
            {
                item.TimeToLive = timeToLive;

                await RawSetAsync(key, item, timeToLive).ConfigureAwait(false);
            }
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(_cache.Contains(key));
        }

        public Task RawSetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            if (timeToLive != null)
            {
                _cache.Set(key, value, DateTimeOffset.UtcNow.Add(timeToLive.Value));
            }
            else
            {
                _cache.Set(key, value, new CacheItemPolicy());
            }

            return Task.FromResult(0);
        }

        public Task<T> RawGetAsync<T>(string key)
        {
            var item = _cache.GetCacheItem(key);
            return Task.FromResult(item != null ? ((T)item.Value) : default(T));
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            var item = await RawGetAsync<object>(key).ConfigureAwait(false) as IInProcessCacheWrapper;

            return item == null ? null : item.TimeToLive;
        }

        public string GetUniqueIdentifier()
        {
            return $"InProcessCache.{_cache.Name}";
        }

        private class InProcessCacheWrapper<T> : CacheWrapper<T>, IInProcessCacheWrapper
        {
            private DateTimeOffset? ExpireAt { get; set; }

            public TimeSpan? TimeToLive
            {
                get
                {
                    return ExpireAt == null
                        ? default(TimeSpan?)
                        : ExpireAt.Value.Subtract(DateTimeOffset.UtcNow);
                }
                set
                {
                    if (value != null)
                    {
                        ExpireAt = DateTimeOffset.UtcNow.Add(value.Value);
                    }
                }
            }

            private InProcessCacheWrapper(T value, TimeSpan? timeToLive = null) : base(value)
            {
                TimeToLive = timeToLive;
            }

            internal static InProcessCacheWrapper<T> For(T value, TimeSpan? timeToLive = null)
            {
                return new InProcessCacheWrapper<T>(value, timeToLive);
            }
        }

        private interface IInProcessCacheWrapper
        {
            TimeSpan? TimeToLive { get; set; }
        }
    }
}
