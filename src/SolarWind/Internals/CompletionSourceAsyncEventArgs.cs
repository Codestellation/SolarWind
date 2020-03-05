using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    internal class CompletionSourceAsyncEventArgs : SocketAsyncEventArgs
    {
        private TaskCompletionSource<int> _source;

        public TaskCompletionSource<int> CompletionSource
        {
            get
            {
                if (_source != null)
                {
                    return _source;
                }
                // Here's possible multiple creation of TaskCompletionSource, but it's unlikely to happen;
                // However it allows thread-safe assigning without locking. 
                Interlocked.CompareExchange(ref _source, new TaskCompletionSource<int>(), null);
                return _source;
            }
        }
    }
}