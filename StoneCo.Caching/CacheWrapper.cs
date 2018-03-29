namespace StoneCo.Caching
{
    internal class CacheWrapper<T>
    {
        public T Value { get; private set; }

        public CacheWrapper(T value)
        {
            Value = value;
        }

        internal static CacheWrapper<T> For(T value)
        {
            return new CacheWrapper<T>(value);
        }
    }
}
