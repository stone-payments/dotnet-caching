using System;
using Vtex.Caching.Enums;

namespace Vtex.Caching
{
    public class CacheKeyEvent
    {
        public string CacheKey { get; set; }

        public EventType EventType { get; set; }

        public string CacheBackendIdentifier { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
