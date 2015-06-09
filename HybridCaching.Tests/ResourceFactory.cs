using System;
using System.Collections.Generic;
using System.Linq;
using HybridCaching.Backends.InProcess;
using HybridCaching.Backends.Redis;
using HybridCaching.Interfaces;
using StackExchange.Redis;

namespace HybridCaching.Tests
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
            return new HybridCache(GetCacheStack());
        }

        public static HybridCache GetHybridCache(params IRawCache[] cacheBackends)
        {
            return new HybridCache(GetCacheStack(cacheBackends));
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
