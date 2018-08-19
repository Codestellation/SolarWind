using System;
using System.Collections.Concurrent;
using System.ComponentModel.Design.Serialization;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind.Clients
{
    public class SolarWindClient
    {
        private readonly Channel _channel;
        private readonly ConcurrentDictionary<MessageId, object> _requestRegistry;

        public SolarWindClient(Channel channel)
        {
           _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            channel.SetCallback(OnSolarWindCallback);
            _requestRegistry = new ConcurrentDictionary<MessageId, object>();
        }
        /// <summary>
        /// Do not use this method if you don't need a response. In such a case use  <see cref="NotifyAsync{TNotification}"/> method.
        /// </summary>
        /// <param name="request"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <returns></returns>
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            MessageId id = _channel.Post(request);
            ClientCompletionSource<TResponse> source = ClientCompletionSource<TResponse>.Rent(id);
            object result = _requestRegistry.GetOrAdd(id, msgId => source);
            //It's highly unlikely but possible that response will come before adding completion source in the dictionary
            // In such a case return the value immediately
            if (result is TResponse response)
            {
                _requestRegistry.TryRemove(id, out _); //Simply remove the value from the dictionary
                source.Dispose(); // Returns source to the pool
                return new ValueTask<TResponse>(response);
            }

            //TODO: Use pooled value task completion source to avoid excessive allocations
            return new ValueTask<TResponse>(source, source.Version);
        }

        public void NotifyAsync<TNotification>(TNotification notification) => _channel.Post(notification);

        private void OnSolarWindCallback(Channel channel, in MessageHeader header, object data)
        {
            // It's possible that result will be available before task completion source was added to the registry
            // so put the result itself into the registry.
            object result = _requestRegistry.GetOrAdd(header.ReplyTo, data);
            //Well, normal flow of operation - set result and remove completion source from the registry.
            if (result is IClientCompletionSource source)
            {
                //Note: The source is return to pool when GetResult is called during Reset procedure, see AutoResetValueTaskSource
                _requestRegistry.TryRemove(header.ReplyTo, out _);
                source.SetGenericResult(data);

            }
        }

    }
}