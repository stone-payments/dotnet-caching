using StoneCo.Caching.Backends.InProcess;
using StoneCo.Caching.Backends.Redis;
using StoneCo.Caching.Configuration;
using StoneCo.Caching.Interfaces;
using System.Collections.Generic;
using System.Reflection;

namespace StoneCo.Caching.Factories
{
    public static class BackendFactory
    {
        public static IEnumerable<IRawCache> CreateRawCache(CachingConfiguration configuration)
        {
            var caches = new List<IRawCache>();

            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

            if (!string.IsNullOrWhiteSpace(configuration.RedisConnectionString))
            {
                caches.Add(new RedisCache(configuration.RedisConnectionString, assemblyName));
            }

            caches.Add(new InProcessCache());

            return caches;
        }
    }
}
