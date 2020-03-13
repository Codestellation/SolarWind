using Microsoft.Extensions.ObjectPool;

namespace Codestellation.SolarWind.Internals
{
    public class MemoryStreamPool : DefaultObjectPool<PooledMemoryStream>
    {
        public static readonly MemoryStreamPool Instance = new MemoryStreamPool(1024);

        private class PoolPolicy : IPooledObjectPolicy<PooledMemoryStream>
        {
            public static readonly PoolPolicy Default = new PoolPolicy();
            public PooledMemoryStream Create() => new PooledMemoryStream();

            public bool Return(PooledMemoryStream obj)
            {
                obj.Reset(obj.Length > 1024);
                return true;
            }
        }

        public MemoryStreamPool() : base(PoolPolicy.Default)
        {
        }

        public MemoryStreamPool(int maximumRetained) : base(PoolPolicy.Default, maximumRetained)
        {
        }
    }
}