using System;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind.Clients
{
    internal class ClientCompletionSource<TResponse> : AutoResetValueTaskSource<TResponse>, IClientCompletionSource, IDisposable
    {
        private static readonly StackBasedPool<ClientCompletionSource<TResponse>> Pool = new StackBasedPool<ClientCompletionSource<TResponse>>(() => new ClientCompletionSource<TResponse>());


        public MessageId MessageId { get; private set; }

        private ClientCompletionSource()
        {
        }

        public static ClientCompletionSource<TResponse> Rent(MessageId id)
        {
            ClientCompletionSource<TResponse> result = Pool.Rent();
            result.MessageId = id;
            return result;
        }

        public void Dispose()
        {
            MessageId = MessageId.Empty;
            Reset();
            Pool.Return(this);
        }

        public void SetGenericResult(object data) => SetResult((TResponse)data);
        public bool TrySetTimeout(TimeSpan timeout) => SetException(new TimeoutException());
    }
}