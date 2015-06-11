using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vtex.Caching.Enums;
using Vtex.Caching.Interfaces;

namespace Vtex.Caching
{
    public class HybridCache : IHybridCache
    {
        private readonly Stack<IRawCache> _cacheBackends;

        public HybridCache(Stack<IRawCache> cacheBackends)
        {
            this._cacheBackends = cacheBackends;

            this.SubscribeAsync(cacheBackends).Wait();
        }

        private async Task SubscribeAsync(IEnumerable<IRawCache> cacheBackends)
        {
            var parentBackends = new List<IRawCache>();

            foreach (var backend in cacheBackends)
            {
                var subscribableBackend = backend as ISubscribable;

                if (subscribableBackend != null)
                {
                    IRawCache currentBackend = backend;

                    var targetBackends = new List<IRawCache>(parentBackends);

                    await subscribableBackend.SubscribeToDeleteAsync(
                        (eventType, keyName) => PropagateEvent(keyName, PropagationAction.Delete, currentBackend,
                                    targetBackends)).ConfigureAwait(false);

                    await subscribableBackend.SubscribeToUpdateTimeToLiveAsync(
                        (eventType, keyName) => PropagateEvent(keyName, PropagationAction.UpdateTimeToLive, currentBackend,
                                    targetBackends)).ConfigureAwait(false);
                }

                parentBackends.Add(backend);
            }
        }

        private void PropagateEvent(string keyName, PropagationAction action, IRawCache originBackend, 
            IEnumerable<IRawCache> targetBackends)
        {
            switch (action)
            {
                case PropagationAction.Delete:
                {
                    var deleteTasks = targetBackends.Select(backend => backend.DeleteAsync(keyName));

                    Task.WhenAll(deleteTasks).Wait();

                    break;
                }
                case PropagationAction.UpdateTimeToLive:
                {
                    var timeToLive = originBackend.GetTimeToLiveAsync(keyName).Result;

                    var updateTimeToLiveTasks = targetBackends.Select(backend => backend.ExpireInAsync(keyName, timeToLive));

                    Task.WhenAll(updateTimeToLiveTasks).Wait();

                    break;
                }
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync)
        {
            var item = await this.GetWrappedAsync<T>(key).ConfigureAwait(false);

            if (item != null)
            {
                return item.Value;
            }

            var value = await createAsync().ConfigureAwait(false);

            await SetAllAsync(this._cacheBackends, key, value, timeToLive).ConfigureAwait(false);

            return value;
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<Dictionary<string, T>>> createManyAsync)
        {
            var item = await this.GetWrappedAsync<T>(key).ConfigureAwait(false);

            if (item != null)
            {
                return item.Value;
            }

            var values = await createManyAsync().ConfigureAwait(false);

            var setAllTasks = values.Select(kv => SetAllAsync(this._cacheBackends, kv.Key, kv.Value, timeToLive));

            await Task.WhenAll(setAllTasks).ConfigureAwait(false);

            return values.ContainsKey(key) ? values[key] : default(T);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var entry = await this.GetWrappedAsync<T>(key).ConfigureAwait(false);

            return entry == null ? default(T) : entry.Value;
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            return SetAllAsync(this._cacheBackends, key, value, timeToLive);
        }

        private async Task<CacheWrapper<T>> GetWrappedAsync<T>(string key)
        {
            var cacheBackends = new Stack<IRawCache>(this._cacheBackends.Reverse());
            var cacheMissBackends = new List<IRawCache>();
            CacheWrapper<T> entry = null;

            while (entry == null && cacheBackends.Count != 0)
            {
                var currentBackend = cacheBackends.Pop();

                entry = await currentBackend.RawGetAsync<CacheWrapper<T>>(key);

                if (entry == null)
                {
                    if (cacheBackends.Count != 0)
                    {
                        cacheMissBackends.Add(currentBackend);
                    }
                }
                else if (cacheMissBackends.Count != 0)
                {
                    var timeToLive = await currentBackend.GetTimeToLiveAsync(key).ConfigureAwait(false);

                    await SetAllAsync(cacheMissBackends, key, entry.Value, timeToLive).ConfigureAwait(false);
                }
            }

            return entry;
        }

        private static async Task SetAllAsync<T>(IEnumerable<IRawCache> cacheBackends, string key, T value, TimeSpan? timeToLive)
        {
            var cacheSetTasks =
                cacheBackends.Select(currentMissedBackend => currentMissedBackend.SetAsync(key, value, timeToLive))
                    .ToList();

            await Task.WhenAll(cacheSetTasks).ConfigureAwait(false);
        }
    }
}
