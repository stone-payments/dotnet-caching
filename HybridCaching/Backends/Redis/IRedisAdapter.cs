using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HybridCaching.Backends.Redis
{
    public interface IRedisAdapter
    {
        Task<bool> SaveStringAsync(string key, string value, TimeSpan? expiry = null);
        Task<string> GetStringAsync(string key);
        Task<bool> SaveJsonAsync<T>(string key, T value, TimeSpan? expiry = null);
        Task<T> GetJsonAsync<T>(string key);
        Task<bool> SetAddStringAsync(string key, string value);
        IEnumerable<string> SetScanString(string key);
        Task<bool> SetAddJsonAsync<T>(string key, T value);
        IEnumerable<T> SetScanJson<T>(string key);
        Task<bool> DeleteKeyAsync(string key);
        Task<bool> ExpireKeyAsync(string key, TimeSpan? expiry = null);
        Task<bool> ExpireKeyAsync(string key, DateTime? date);
        Task<TimeSpan?> GetTimeToLiveAsync(string key);
        Task<bool> Exists(string key);
        Task SubscribeAsync<T>(string channelName, Action<string, T> callback);
        Task<long> PublishStringAsync(string channelName, string value);
        Task<long> PublishJsonAsync<T>(string channelName, T value);
        Task SubscribeToKeySpaceAsync(string keySpace, Action<string, string> callback, int database = 0);
        Task SubscribeToKeySpaceAsync(IEnumerable<string> keySpaces, Action<string, string> callback, int database = 0);
        Task SubscribeToKeyEventAsync(string keyEvent, Action<string, string> callback, int database = 0);
        Task SubscribeToKeyEventAsync(IEnumerable<string> keyEvents, Action<string, string> callback, int database = 0);
    }
}