namespace Vtex.Caching
{
    internal class CacheWrapper<T>
    {
        public T Value { get; private set; }

        public CacheWrapper(T value)
        {
            this.Value = value;
        }

        internal static CacheWrapper<T> For(T value)
        {
            return new CacheWrapper<T>(value);
        }
    }
}
