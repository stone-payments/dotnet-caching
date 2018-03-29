using System;
using StoneCo.Caching.Enums;

namespace StoneCo.Caching
{
    public class CacheKeyEvent
    {
        public string CacheKey { get; set; }

        public EventType EventType { get; set; }

        public string CacheBackendIdentifier { get; set; }

        public DateTimeOffset Timestamp { get; set; }
    }
}
