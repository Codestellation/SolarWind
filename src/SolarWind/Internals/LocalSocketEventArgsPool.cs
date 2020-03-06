#if NETSTANDARD2_0
using Microsoft.Extensions.ObjectPool;

namespace Codestellation.SolarWind.Internals
{
    internal class SocketEventArgsPool : DefaultObjectPool<CompletionSourceAsyncEventArgs>
    {
        public static readonly SocketEventArgsPool Instance = new SocketEventArgsPool();

        private class Policy : IPooledObjectPolicy<CompletionSourceAsyncEventArgs>
        {
            public CompletionSourceAsyncEventArgs Create()
            {
                var result = new CompletionSourceAsyncEventArgs();
                result.Completed += AsyncNetworkStream.HandleAsyncResult;
                return result;
            }

            public bool Return(CompletionSourceAsyncEventArgs obj) => true;
        }

        public SocketEventArgsPool()
            : base(new Policy())
        {
        }

        public SocketEventArgsPool(int maximumRetained)
            : base(new Policy(), maximumRetained)
        {
        }

        public override CompletionSourceAsyncEventArgs Get()
        {
            CompletionSourceAsyncEventArgs result = base.Get();
            result.Reset();
            return result;
        }
    }
}
#endif