using System;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;
using Vtex.Caching.Backends.InProcess;
using Vtex.Caching.Backends.Redis;
using Vtex.Caching.Interfaces;
using Vtex.RabbitMQ.Messaging;
using Vtex.RabbitMQ.Messaging.Interfaces;

namespace Vtex.Caching.Tests
{
    public static class ResourceFactory
    {
        public static IRedisAdapter GetRedisAdapter()
        {
            var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");

            return new RedisAdapter(multiplexer);
        }

        public static RedisCache GetRedisCache()
        {
            return new RedisCache(GetRedisAdapter());
        }

        public static InProcessCache GetInProcessCache()
        {
            return new InProcessCache("IntegrationTests");
        }

        public static Stack<IRawCache> GetCacheStack()
        {
            return new Stack<IRawCache>(new List<IRawCache>{GetRedisCache(), GetInProcessCache()});
        }

        public static Stack<IRawCache> GetCacheStack(params IRawCache[] cacheBackends)
        {
            return new Stack<IRawCache>(cacheBackends);
        }

        public static HybridCache GetHybridCache()
        {
            return new HybridCache(GetCacheStack(), GetQueueClient());
        }

        public static HybridCache GetHybridCache(params IRawCache[] cacheBackends)
        {
            return new HybridCache(GetCacheStack(cacheBackends), GetQueueClient());
        }

        public static IQueueClient GetQueueClient()
        {
            return new RabbitMQClient("guest:guest@localhost:5672/testing");
        }

        private static string GenerateString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random((int)DateTime.Now.Ticks);
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static string GenerateKey()
        {
            return String.Format("testKey:{0}", GenerateString(8));
        }

        public static Dictionary<string, string> GenerateDictionary(int size)
        {
            var dictionary = new Dictionary<string, string>();

            for (var i = 0; i < size; i++)
            {
                dictionary.Add(GenerateKey() + i, GenerateString(10));
            }

            return dictionary;
        }
    }
}
