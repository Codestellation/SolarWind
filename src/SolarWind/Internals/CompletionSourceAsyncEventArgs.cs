using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind.Internals
{
    internal class CompletionSourceAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource
    {
        private SyncValueTaskSourceCore _valueSource = new SyncValueTaskSourceCore();

        public ValueTask Task
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ValueTask(this, _valueSource.Version);
        }

        public void Reset()
            => _valueSource.Reset();

        public void SetException(Exception exception)
            => _valueSource.SetException(exception);

        public void SetResult()
            => _valueSource.SetResult();

        public ValueTaskSourceStatus GetStatus(short token)
            => _valueSource.GetStatus(token);

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _valueSource.OnCompleted(continuation, state, token, flags);

        public void GetResult(short token)
            => _valueSource.GetResult(token);
    }
}