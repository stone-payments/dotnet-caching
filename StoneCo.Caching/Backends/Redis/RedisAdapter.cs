using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace StoneCo.Caching.Backends.Redis
{
    public class RedisAdapter : IRedisAdapter
    {
        private readonly ConnectionMultiplexer _multiplexer;
        private readonly string _keyPrefix;
        private IDatabase Database { get { return _multiplexer.GetDatabase(); } }

        public RedisAdapter(ConnectionMultiplexer multiplexer, string keyPrefix = null)
        {
            _multiplexer = multiplexer;
            _keyPrefix = keyPrefix;
        }

        public RedisAdapter(string redisEndpoint, string keyPrefix = null)
        {
            _multiplexer = ConnectionMultiplexer.Connect(redisEndpoint);
            _keyPrefix = keyPrefix;
        }

        private RedisKey Prepare(string key)
        {
            return _keyPrefix == null ? key : _keyPrefix + "." + key;
        }

        public Task<bool> SaveStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            return Database.StringSetAsync(Prepare(key), value, expiry);
        }

        public async Task<string> GetStringAsync(string key)
        {
            var redisValue = await Database.StringGetAsync(Prepare(key)).ConfigureAwait(false);

            return redisValue.ToString();
        }

        public Task<bool> SaveJsonAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var serializedValue = JsonConvert.SerializeObject(value);

            return SaveStringAsync(key, serializedValue, expiry);
        }

        public async Task<T> GetJsonAsync<T>(string key)
        {
            var serializedValue = await GetStringAsync(key).ConfigureAwait(false);

            var value = default(T);

            if (serializedValue != null)
            {
                value = JsonConvert.DeserializeObject<T>(serializedValue);
            }

            return value;
        }

        public Task<bool> SetAddStringAsync(string key, string value)
        {
            return Database.SetAddAsync(Prepare(key), value);
        }

        public IEnumerable<string> SetScanString(string key)
        {
            return Database.SetScan(Prepare(key)).Select(value => value.ToString());
        }

        public Task<bool> SetAddJsonAsync<T>(string key, T value)
        {
            var serializedValue = JsonConvert.SerializeObject(value);

            return SetAddStringAsync(key, serializedValue);
        }

        public IEnumerable<T> SetScanJson<T>(string key)
        {
            return Database.SetScan(Prepare(key)).Select(value => JsonConvert.DeserializeObject<T>(value));
        }

        public Task<bool> DeleteKeyAsync(string key)
        {
            return Database.KeyDeleteAsync(Prepare(key));
        }

        public Task<bool> ExpireKeyAsync(string key, TimeSpan? expiry = null)
        {
            if (expiry == null)
            {
                expiry = TimeSpan.Zero;
            }

            return Database.KeyExpireAsync(key, expiry);
        }

        public Task<bool> ExpireKeyAsync(string key, DateTime? date)
        {
            return Database.KeyExpireAsync(key, date);
        }

        public Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            return Database.KeyTimeToLiveAsync(key);
        }

        public Task<bool> ExistsAsync(string key)
        {
            return Database.KeyExistsAsync(key);
        }

        public Task SubscribeAsync<T>(string channelName, Action<string, T> callback)
        {
            var subscriber = _multiplexer.GetSubscriber();

            return
                subscriber.SubscribeAsync(channelName,
                    (eventChannel, eventValue) =>
                        callback(eventChannel,
                            (typeof (T) == typeof (string)
                                ? (T)(object)eventValue.ToString()
                                : JsonConvert.DeserializeObject<T>(eventValue))));
        }

        public Task<long> PublishStringAsync(string channelName, string value)
        {
            return Database.PublishAsync(channelName, value);
        }

        public Task<long> PublishJsonAsync<T>(string channelName, T value)
        {
            var serializedValue = JsonConvert.SerializeObject(value);

            return PublishStringAsync(channelName, serializedValue);
        }

        public Task SubscribeToKeySpaceAsync(string keySpace, Action<string, string> callback, int database = 0)
        {
            var channel = String.Format("__keyspace@{0}__:{1}", database, keySpace);

            return SubscribeAsync(channel, callback);
        }

        public Task SubscribeToKeySpaceAsync(IEnumerable<string> keySpaces, Action<string, string> callback,
            int database = 0)
        {
            var subscriptionTasks =
                keySpaces.Select(keySpace => SubscribeToKeySpaceAsync(keySpace, callback, database));

            return Task.WhenAll(subscriptionTasks);
        }

        public Task SubscribeToKeyEventAsync(string keyEvent, Action<string, string> callback, int database = 0)
        {
            var channel = String.Format("__keyevent@{0}__:{1}", database, keyEvent);

            return SubscribeAsync(channel, callback);
        }

        public Task SubscribeToKeyEventAsync(IEnumerable<string> keyEvents, Action<string, string> callback,
            int database = 0)
        {
            var subscriptionTasks =
                keyEvents.Select(keyEvent => SubscribeToKeyEventAsync(keyEvent, callback, database));

            return Task.WhenAll(subscriptionTasks);
        }

        public string GetEndpoint()
        {
            return _multiplexer.Configuration;
        }
    }
}
