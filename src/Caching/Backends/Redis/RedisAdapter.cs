using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace Vtex.Caching.Backends.Redis
{
    public class RedisAdapter : IRedisAdapter
    {
        private readonly ConnectionMultiplexer _multiplexer;
        private readonly string _keyPrefix;
        private IDatabase Database { get { return this._multiplexer.GetDatabase(); } }

        public RedisAdapter(ConnectionMultiplexer multiplexer, string keyPrefix = null)
        {
            this._multiplexer = multiplexer;
            this._keyPrefix = keyPrefix;
        }

        public RedisAdapter(string redisEndpoint, string keyPrefix = null)
        {
            this._multiplexer = ConnectionMultiplexer.Connect(redisEndpoint);
            this._keyPrefix = keyPrefix;
        }

        private RedisKey Prepare(string key)
        {
            return this._keyPrefix == null ? key : this._keyPrefix + "." + key;
        }

        public Task<bool> SaveStringAsync(string key, string value, TimeSpan? expiry = null)
        {
            return this.Database.StringSetAsync(Prepare(key), value, expiry);
        }

        public async Task<string> GetStringAsync(string key)
        {
            var redisValue = await this.Database.StringGetAsync(Prepare(key)).ConfigureAwait(false);

            return redisValue.ToString();
        }

        public Task<bool> SaveJsonAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var serializedValue = JsonConvert.SerializeObject(value);

            return this.SaveStringAsync(key, serializedValue, expiry);
        }

        public async Task<T> GetJsonAsync<T>(string key)
        {
            var serializedValue = await this.GetStringAsync(key).ConfigureAwait(false);

            var value = default(T);

            if (serializedValue != null)
            {
                value = JsonConvert.DeserializeObject<T>(serializedValue);
            }

            return value;
        }

        public Task<bool> SetAddStringAsync(string key, string value)
        {
            return this.Database.SetAddAsync(Prepare(key), value);
        }

        public IEnumerable<string> SetScanString(string key)
        {
            return this.Database.SetScan(Prepare(key)).Select(value => value.ToString());
        }

        public Task<bool> SetAddJsonAsync<T>(string key, T value)
        {
            var serializedValue = JsonConvert.SerializeObject(value);

            return this.SetAddStringAsync(key, serializedValue);
        }

        public IEnumerable<T> SetScanJson<T>(string key)
        {
            return this.Database.SetScan(Prepare(key)).Select(value => JsonConvert.DeserializeObject<T>(value));
        }

        public Task<bool> DeleteKeyAsync(string key)
        {
            return this.Database.KeyDeleteAsync(Prepare(key));
        }

        public Task<bool> ExpireKeyAsync(string key, TimeSpan? expiry = null)
        {
            if (expiry == null)
            {
                expiry = TimeSpan.Zero;
            }

            return this.Database.KeyExpireAsync(key, expiry);
        }

        public Task<bool> ExpireKeyAsync(string key, DateTime? date)
        {
            return this.Database.KeyExpireAsync(key, date);
        }

        public Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            return this.Database.KeyTimeToLiveAsync(key);
        }

        public Task<bool> ExistsAsync(string key)
        {
            return this.Database.KeyExistsAsync(key);
        }

        public Task SubscribeAsync<T>(string channelName, Action<string, T> callback)
        {
            var subscriber = this._multiplexer.GetSubscriber();

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
            return this.Database.PublishAsync(channelName, value);
        }

        public Task<long> PublishJsonAsync<T>(string channelName, T value)
        {
            var serializedValue = JsonConvert.SerializeObject(value);

            return this.PublishStringAsync(channelName, serializedValue);
        }

        public Task SubscribeToKeySpaceAsync(string keySpace, Action<string, string> callback, int database = 0)
        {
            var channel = String.Format("__keyspace@{0}__:{1}", database, keySpace);

            return this.SubscribeAsync(channel, callback);
        }

        public Task SubscribeToKeySpaceAsync(IEnumerable<string> keySpaces, Action<string, string> callback,
            int database = 0)
        {
            var subscriptionTasks =
                keySpaces.Select(keySpace => this.SubscribeToKeySpaceAsync(keySpace, callback, database));

            return Task.WhenAll(subscriptionTasks);
        }

        public Task SubscribeToKeyEventAsync(string keyEvent, Action<string, string> callback, int database = 0)
        {
            var channel = String.Format("__keyevent@{0}__:{1}", database, keyEvent);

            return this.SubscribeAsync(channel, callback);
        }

        public Task SubscribeToKeyEventAsync(IEnumerable<string> keyEvents, Action<string, string> callback,
            int database = 0)
        {
            var subscriptionTasks =
                keyEvents.Select(keyEvent => this.SubscribeToKeyEventAsync(keyEvent, callback, database));

            return Task.WhenAll(subscriptionTasks);
        }

        public string GetEndpoint()
        {
            return _multiplexer.Configuration;
        }
    }
}
