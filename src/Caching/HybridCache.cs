using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vtex.Caching.Backends.InProcess;
using Vtex.Caching.Backends.Redis;
using Vtex.Caching.Enums;
using Vtex.Caching.Interfaces;
using Vtex.RabbitMQ.Messaging;
using Vtex.RabbitMQ.Messaging.Interfaces;
using Vtex.RabbitMQ.ProcessingWorkers;
using static System.String;

namespace Vtex.Caching
{
    public class HybridCache : IHybridCache
    {
        private const string EventListeningQueuePrefix = "hybrid-cache.propagate-event";

        private const string EventPublishingExchange = "hybrid-cache";

        private const string EventPublishingRoute = "new-event";

        private readonly Stack<IRawCache> _cacheBackends;

        private readonly IQueueClient _queueClient;

        private readonly AdvancedAsyncMessageProcessingWorker<CacheKeyEvent> _messageProcessingWorker;

        public delegate Task<AdvancedAsyncMessageProcessingWorker<CacheKeyEvent>> CreateConsumerWorkerAsync();

        public HybridCache(IQueueClient queueClient = null, string instanceUniqueIdentifier = null, 
            Func<CreateConsumerWorkerAsync, Task<AdvancedAsyncMessageProcessingWorker<CacheKeyEvent>>> consumerWrapper = null)
            : this(GetDefaultCacheBackends(), queueClient, instanceUniqueIdentifier, consumerWrapper)
        {
        }

        public HybridCache(Stack<IRawCache> cacheBackends, IQueueClient queueClient = null, string instanceUniqueIdentifier = null,
            Func<CreateConsumerWorkerAsync, Task<AdvancedAsyncMessageProcessingWorker<CacheKeyEvent>>> consumerWrapper = null)
        {
            this._cacheBackends = cacheBackends;

            if (queueClient == null)
            {
                var rabbitMqEndpoint = ConfigurationManager.AppSettings["vtex.caching:rabbitmq-endpoint"];
                _queueClient = new RabbitMQClient(rabbitMqEndpoint);
            }
            else
            {
                _queueClient = queueClient;
            }

            instanceUniqueIdentifier = IsNullOrWhiteSpace(instanceUniqueIdentifier) ? Guid.NewGuid().ToString() : instanceUniqueIdentifier;

            var queueName = $"{EventListeningQueuePrefix}.{instanceUniqueIdentifier}";

            EnsureQueueAndBindings(queueName);

            CreateConsumerWorkerAsync createWorker = () => AdvancedAsyncMessageProcessingWorker<CacheKeyEvent>.CreateAndStartAsync(_queueClient, queueName,
                    PropagateEventAsync, TimeSpan.FromSeconds(1), CancellationToken.None);

            _messageProcessingWorker = consumerWrapper == null ? createWorker().Result : consumerWrapper(createWorker).Result;
        }

        private static Stack<IRawCache> GetDefaultCacheBackends()
        {
            var cacheBackends = new Stack<IRawCache>();

            var assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;

            var redisEndpoint = ConfigurationManager.AppSettings["vtex.caching:redis-endpoint"];

            if (!IsNullOrEmpty(redisEndpoint))
            {
                cacheBackends.Push(new RedisCache(redisEndpoint, assemblyName));
            }

            cacheBackends.Push(new InProcessCache(assemblyName));
            return cacheBackends;
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

        public async Task DeleteAsync(string key)
        {
            var cacheDeletionTasks =
                this._cacheBackends.Select(currentMissedBackend => currentMissedBackend.DeleteAsync(key))
                    .ToList();

            await Task.WhenAll(cacheDeletionTasks).ConfigureAwait(false);

            PublishEvent(key, EventType.Delete, _cacheBackends.Last().GetUniqueIdentifier());
        }

        public void Dispose()
        {
            _messageProcessingWorker.Dispose();
        }

        private void EnsureQueueAndBindings(string queueName)
        {
            var arguments = new Dictionary<string, object> { { "x-expires", (long)TimeSpan.FromHours(1).TotalMilliseconds } };

            _queueClient.QueueDeclare(queueName, arguments: arguments);

            _queueClient.ExchangeDeclare(EventPublishingExchange);

            _queueClient.QueueBind(queueName, EventPublishingExchange, EventPublishingRoute);
        }

        private Task PropagateEventAsync(CacheKeyEvent cacheKeyEvent, CancellationToken cancellationToken)
        {
            var elegibleBackends = _cacheBackends.TakeWhile(
                backend => backend.GetUniqueIdentifier() != cacheKeyEvent.CacheBackendIdentifier);

            var deleteTasks = elegibleBackends.Select(backend => backend.DeleteAsync(cacheKeyEvent.CacheKey));

            return Task.WhenAll(deleteTasks);
        }

        private void PublishEvent(string cacheKey, EventType eventType, string backendUniqueIdentifier)
        {
            var cacheKeyEvent = new CacheKeyEvent { CacheKey = cacheKey, EventType = eventType, CacheBackendIdentifier = backendUniqueIdentifier };

            _queueClient.Publish(EventPublishingExchange, EventPublishingRoute, cacheKeyEvent);
        }

        private async Task<CacheWrapper<T>> GetWrappedAsync<T>(string key)
        {
            var cacheBackends = new Stack<IRawCache>(this._cacheBackends.Reverse());
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
