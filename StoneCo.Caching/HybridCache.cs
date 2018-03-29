using RabbitMQ.Abstraction.Messaging.Interfaces;
using RabbitMQ.Abstraction.ProcessingWorkers;
using StoneCo.Caching.Configuration;
using StoneCo.Caching.Enums;
using StoneCo.Caching.Factories;
using StoneCo.Caching.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StoneCo.Caching
{
    public class HybridCache : IHybridCache
    {
        private const string EventListeningQueuePrefix = "hybrid-cache.propagate-event";

        private const string EventPublishingExchange = "hybrid-cache";

        private const string EventPublishingRoute = "new-event";

        private readonly Stack<IRawCache> _cacheBackends;

        private readonly IQueueClient _queueClient;

        private readonly AdvancedAsyncProcessingWorker<CacheKeyEvent> _messageProcessingWorker;

        public static CachingConfiguration Configuration { get; private set; }

        public HybridCache(IQueueClient queueClient = null,
            Func<IMessageProcessingWorker<CacheKeyEvent>, Task> startWorkerAsync = null,
            string instanceUniqueIdentifier = null)
            : this(GetDefaultCacheBackends(), queueClient, startWorkerAsync, instanceUniqueIdentifier)
        {
        }

        public HybridCache(Stack<IRawCache> cacheBackends, IQueueClient queueClient,
            Func<IMessageProcessingWorker<CacheKeyEvent>, Task> startWorkerAsync = null,
            string instanceUniqueIdentifier = null)
        {
            _cacheBackends = cacheBackends;

            _queueClient = queueClient;

            instanceUniqueIdentifier = string.IsNullOrWhiteSpace(instanceUniqueIdentifier)
                ? Guid.NewGuid().ToString()
                : instanceUniqueIdentifier;

            var queueName = $"{EventListeningQueuePrefix}.{instanceUniqueIdentifier}";

            EnsureQueueAndBindingsAsync(queueName).Wait();

            _messageProcessingWorker =
                new AdvancedAsyncProcessingWorker<CacheKeyEvent>(_queueClient, queueName,
                    PropagateEventAsync, TimeSpan.FromSeconds(1));

            if (startWorkerAsync == null)
            {
                _messageProcessingWorker.StartAsync(CancellationToken.None).Wait();
            }
            else
            {
                startWorkerAsync(_messageProcessingWorker).Wait();
            }
        }

        public void LoadConfiguration(CachingConfiguration configuration)
        {
            if (configuration == null)
            {
                configuration = new CachingConfiguration();
            }

            Configuration = configuration;
        }

        private static Stack<IRawCache> GetDefaultCacheBackends()
        {
            var cacheBackends = new Stack<IRawCache>();

            foreach (var cache in BackendFactory.CreateRawCache(Configuration))
            {
                cacheBackends.Push(cache);
            }

            return cacheBackends;
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<T>> createAsync)
        {
            var item = await GetWrappedAsync<T>(key).ConfigureAwait(false);

            if (item != null)
            {
                return item.Value;
            }

            var value = await createAsync().ConfigureAwait(false);

            await SetAllAsync(_cacheBackends, key, value, timeToLive).ConfigureAwait(false);

            return value;
        }

        public async Task<T> GetOrSetAsync<T>(string key, TimeSpan? timeToLive, Func<Task<Dictionary<string, T>>> createManyAsync)
        {
            var item = await GetWrappedAsync<T>(key).ConfigureAwait(false);

            if (item != null)
            {
                return item.Value;
            }

            var values = await createManyAsync().ConfigureAwait(false);

            var setAllTasks = values.Select(kv => SetAllAsync(_cacheBackends, kv.Key, kv.Value, timeToLive));

            await Task.WhenAll(setAllTasks).ConfigureAwait(false);

            return values.ContainsKey(key) ? values[key] : default(T);
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var entry = await GetWrappedAsync<T>(key).ConfigureAwait(false);

            return entry == null ? default(T) : entry.Value;
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? timeToLive)
        {
            return SetAllAsync(_cacheBackends, key, value, timeToLive);
        }

        public async Task DeleteAsync(string key)
        {
            var cacheDeletionTasks =
                _cacheBackends.Select(currentMissedBackend => currentMissedBackend.DeleteAsync(key))
                    .ToList();

            await Task.WhenAll(cacheDeletionTasks).ConfigureAwait(false);

            await PublishEventAsync(key, EventType.Delete, _cacheBackends.Last().GetUniqueIdentifier());
        }

        public void Dispose()
        {
            _messageProcessingWorker.Dispose();
        }

        private async Task EnsureQueueAndBindingsAsync(string queueName)
        {
            var arguments = new Dictionary<string, object> { { "x-expires", (long)TimeSpan.FromHours(1).TotalMilliseconds } };

            await _queueClient.QueueDeclareAsync(queueName, arguments: arguments);

            await _queueClient.ExchangeDeclareAsync(EventPublishingExchange);

            await _queueClient.QueueBindAsync(queueName, EventPublishingExchange, EventPublishingRoute);
        }

        private Task PropagateEventAsync(CacheKeyEvent cacheKeyEvent, CancellationToken cancellationToken)
        {
            var elegibleBackends = _cacheBackends.TakeWhile(
                backend => backend.GetUniqueIdentifier() != cacheKeyEvent.CacheBackendIdentifier);

            var deleteTasks = elegibleBackends.Select(backend => backend.DeleteAsync(cacheKeyEvent.CacheKey));

            return Task.WhenAll(deleteTasks);
        }

        private async Task PublishEventAsync(string cacheKey, EventType eventType, string backendUniqueIdentifier)
        {
            var cacheKeyEvent = new CacheKeyEvent { CacheKey = cacheKey, EventType = eventType, CacheBackendIdentifier = backendUniqueIdentifier };

            await _queueClient.PublishAsync(EventPublishingExchange, EventPublishingRoute, cacheKeyEvent);
        }

        private async Task<CacheWrapper<T>> GetWrappedAsync<T>(string key)
        {
            var cacheBackends = new Stack<IRawCache>(_cacheBackends.Reverse());
            var cacheMissBackends = new List<IRawCache>();
            CacheWrapper<T> entry = null;

            while (entry == null && cacheBackends.Count != 0)
            {
                var currentBackend = cacheBackends.Pop();

                entry = await currentBackend.RawGetAsync<CacheWrapper<T>>(key).ConfigureAwait(false);

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
