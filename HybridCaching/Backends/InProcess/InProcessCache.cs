using System;
using System.Runtime.Caching;
using System.Threading.Tasks;
using HybridCaching.Interfaces;

namespace HybridCaching.Backends.InProcess
{
    public class InProcessCache : IRawCache
    {
        private MemoryCache Cache { get; set; }

        public InProcessCache(string name)
        {
            this.Cache = new MemoryCache(name);
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync)
        {
            var item = this.Cache.GetCacheItem(key);
            if (item != null)
                return ((InProcessCacheWrapper<T>)item.Value).Value;

            var value = await createAsync().ConfigureAwait(false);
            await this.SetAsync(key, value, timeToLive).ConfigureAwait(false);
            return value;
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var item = await this.RawGetAsync<InProcessCacheWrapper<T>>(key).ConfigureAwait(false);
            return item != null ? item.Value : default(T);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            await this.RawSetAsync(key, InProcessCacheWrapper<T>.For(value, timeToLive), timeToLive).ConfigureAwait(false);
        }

        public Task DeleteAsync(string key)
        {
            this.Cache.Remove(key);

            return Task.FromResult(0);
        }

        public async Task ExpireInAsync(string key, TimeSpan? timeToLive)
        {
            var item = await this.RawGetAsync<object>(key).ConfigureAwait(false) as IInProcessCacheWrapper;

            if (item != null)
            {
                item.TimeToLive = timeToLive;

                await this.RawSetAsync(key, item, timeToLive).ConfigureAwait(false);
            }
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Task.FromResult(this.Cache.Contains(key));
        }

        public Task RawSetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            if (timeToLive != null)
            {
                this.Cache.Set(key, value, DateTimeOffset.UtcNow.Add(timeToLive.Value));
            }
            else
            {
                this.Cache.Set(key, value, new CacheItemPolicy());
            }

            return Task.FromResult(0);
        }

        public Task<T> RawGetAsync<T>(string key)
        {
            var item = this.Cache.GetCacheItem(key);
            return Task.FromResult(item != null ? ((T)item.Value) : default(T));
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            var item = await this.RawGetAsync<object>(key).ConfigureAwait(false) as IInProcessCacheWrapper;

            return item == null ? null : item.TimeToLive;
        }

        private class InProcessCacheWrapper<T> : CacheWrapper<T>, IInProcessCacheWrapper
        {
            private DateTimeOffset? ExpireAt { get; set; }

            public TimeSpan? TimeToLive
            {
                get
                {
                    return this.ExpireAt == null
                        ? default(TimeSpan?)
                        : this.ExpireAt.Value.Subtract(DateTimeOffset.UtcNow);
                }
                set
                {
                    if (value != null)
                    {
                        this.ExpireAt = DateTimeOffset.UtcNow.Add(value.Value);
                    }
                }
            }

            private InProcessCacheWrapper(T value, TimeSpan? timeToLive = null) : base(value)
            {
                this.TimeToLive = timeToLive;
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
