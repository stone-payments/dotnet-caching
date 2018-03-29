using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using StoneCo.Caching.Interfaces;

namespace StoneCo.Caching.Backends.Redis
{
    public class RedisCache : IRawCache, ISubscribable
    {
        private IRedisAdapter RedisAdapter { get; set; }

        //TODO: Evicted by "MaxMemory" in redis should propagate delete?
        private readonly List<string> _redisDeleteEvents = new List<string>{"del", "expired", "evicted"};

        private readonly List<string> _redisUpdateTimeToLiveEvents = new List<string>{"expire"};

        public RedisCache(IRedisAdapter redisAdapter)
        {
            RedisAdapter = redisAdapter;
        }

        public RedisCache(string redisEndpoint, string keyPrefix = null)
        {
            RedisAdapter = new RedisAdapter(redisEndpoint, keyPrefix);
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync)
        {
            var item = await GetAsync<CacheWrapper<T>>(key).ConfigureAwait(false);

            if (item != null)
            {
                return item.Value;
            }

            var value = await createAsync().ConfigureAwait(false);
            await SetAsync(key, CacheWrapper<T>.For(value), timeToLive).ConfigureAwait(false);
            return value;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var item = await RawGetAsync<CacheWrapper<T>>(key).ConfigureAwait(false);

            return item != null ? item.Value : default(T);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            await RawSetAsync(key, CacheWrapper<T>.For(value), timeToLive).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string key)
        {
            await RedisAdapter.DeleteKeyAsync(key).ConfigureAwait(false);
        }

        public async Task ExpireInAsync(string key, TimeSpan? timeToLive)
        {
            await RedisAdapter.ExpireKeyAsync(key, timeToLive).ConfigureAwait(false);
        }

        public Task<bool> ExistsAsync(string key)
        {
            return RedisAdapter.ExistsAsync(key);
        }

        public async Task RawSetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            await RedisAdapter.SaveJsonAsync(key, value, timeToLive).ConfigureAwait(false);
        }

        public async Task<T> RawGetAsync<T>(string key)
        {
            return await RedisAdapter.GetJsonAsync<T>(key).ConfigureAwait(false);
        }

        public Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            return RedisAdapter.GetTimeToLiveAsync(key);
        }

        public string GetUniqueIdentifier()
        {
            return $"RedisCache.{RedisAdapter.GetEndpoint()}";
        }

        public async Task SubscribeToDeleteAsync(Action<string, string> callback)
        {
            await RedisAdapter.SubscribeToKeyEventAsync(_redisDeleteEvents, callback).ConfigureAwait(false);
        }

        public async Task SubscribeToUpdateTimeToLiveAsync(Action<string, string> callback)
        {
            await RedisAdapter.SubscribeToKeyEventAsync(_redisUpdateTimeToLiveEvents, callback)
                    .ConfigureAwait(false);
        }
    }
}
