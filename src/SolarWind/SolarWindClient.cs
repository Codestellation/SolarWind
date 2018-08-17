using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public class SolarWindClient
    {
        private readonly Channel _channel;
        private readonly ConcurrentDictionary<MessageId, object> _requestRegistry;

        public SolarWindClient(Channel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _requestRegistry = new ConcurrentDictionary<MessageId, object>();
            channel.SetCallback(OnSolarWindCallback);
        }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            MessageId id = _channel.Post(request);

            object result = _requestRegistry.GetOrAdd(id, msgId => new SolarWindCompleteionSource<TResponse>());
            //It's highly unlikely but possible that response will come before adding completion source in the dictionary
            // In such a case return the value immediately
            if (result is TResponse response)
            {
                _requestRegistry.TryRemove(id, out _); //Simply remove the value from the dictionary
                return new ValueTask<TResponse>(response);
            }

            //TODO: Use pooled value task completion source to avoid excessive allocations
            var completionSource = (TaskCompletionSource<TResponse>)result;
            return new ValueTask<TResponse>(completionSource.Task);
        }

        public void NotifyAsync<TNotification>(TNotification notification) => _channel.Post(notification);

        private void OnSolarWindCallback(Channel channel,in MessageHeader header, object data)
        {
            // It's possible that result will be available before task completion source was added to the registry
            // so put the result itself into the registry. 
            object result = _requestRegistry.GetOrAdd(header.ReplyTo, data);
            //Well, normal flow of operation - set result and remove completion source from the registry. 
            if (result is ISolarWindCompletionSource source)
            {
                source.SetGenericResult(data);
                _requestRegistry.TryRemove(header.ReplyTo, out _);
            }
        }
    }
}