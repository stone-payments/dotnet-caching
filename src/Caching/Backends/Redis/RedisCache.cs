using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vtex.Caching.Interfaces;

namespace Vtex.Caching.Backends.Redis
{
    public class RedisCache : IRawCache, ISubscribable
    {
        private IRedisAdapter RedisAdapter { get; set; }

        //TODO: Evicted by "MaxMemory" in redis should propagate delete?
        private readonly List<string> _redisDeleteEvents = new List<string>{"del", "expired", "evicted"};

        private readonly List<string> _redisUpdateTimeToLiveEvents = new List<string>{"expire"};

        public RedisCache(IRedisAdapter redisAdapter)
        {
            this.RedisAdapter = redisAdapter;
        }

        public RedisCache(string redisEndpoint, string keyPrefix = null)
        {
            this.RedisAdapter = new RedisAdapter(redisEndpoint, keyPrefix);
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync)
        {
            var item = await this.GetAsync<CacheWrapper<T>>(key).ConfigureAwait(false);

            if (item != null)
            {
                return item.Value;
            }

            var value = await createAsync().ConfigureAwait(false);
            await this.SetAsync(key, CacheWrapper<T>.For(value), timeToLive).ConfigureAwait(false);
            return value;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var item = await this.RawGetAsync<CacheWrapper<T>>(key).ConfigureAwait(false);

            return item != null ? item.Value : default(T);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            await this.RawSetAsync(key, CacheWrapper<T>.For(value), timeToLive).ConfigureAwait(false);
        }

        public async Task DeleteAsync(string key)
        {
            await this.RedisAdapter.DeleteKeyAsync(key).ConfigureAwait(false);
        }

        public async Task ExpireInAsync(string key, TimeSpan? timeToLive)
        {
            await this.RedisAdapter.ExpireKeyAsync(key, timeToLive).ConfigureAwait(false);
        }

        public Task<bool> ExistsAsync(string key)
        {
            return this.RedisAdapter.ExistsAsync(key);
        }

        public async Task RawSetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            await this.RedisAdapter.SaveJsonAsync(key, value, timeToLive).ConfigureAwait(false);
        }

        public async Task<T> RawGetAsync<T>(string key)
        {
            return await this.RedisAdapter.GetJsonAsync<T>(key).ConfigureAwait(false);
        }

        public Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            return this.RedisAdapter.GetTimeToLiveAsync(key);
        }

        public string GetUniqueIdentifier()
        {
            return $"RedisCache.{RedisAdapter.GetEndpoint()}";
        }

        public async Task SubscribeToDeleteAsync(Action<string, string> callback)
        {
            await this.RedisAdapter.SubscribeToKeyEventAsync(this._redisDeleteEvents, callback).ConfigureAwait(false);
        }

        public async Task SubscribeToUpdateTimeToLiveAsync(Action<string, string> callback)
        {
            await this.RedisAdapter.SubscribeToKeyEventAsync(this._redisUpdateTimeToLiveEvents, callback)
                    .ConfigureAwait(false);
        }
    }
}
