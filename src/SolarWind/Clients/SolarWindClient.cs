using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind.Clients
{
    public class SolarWindClient : IDisposable
    {
        private readonly Channel _channel;
        private readonly SolarWindClientOptions _options;
        private readonly Action<object> _eventCallback;
        private readonly ConcurrentDictionary<MessageId, object> _requestRegistry;
        private Task _timeoutTask;
        private bool _disposed;

        public SolarWindClient(Channel channel, SolarWindClientOptions options, Action<object> eventCallback)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _eventCallback = eventCallback ?? throw new ArgumentNullException(nameof(eventCallback));
            channel.SetCallback(OnSolarWindCallback);
            _requestRegistry = new ConcurrentDictionary<MessageId, object>();

            _timeoutTask = Task.Run(CheckTimeouts);
        }

        private async void CheckTimeouts()
        {
            while (!_disposed)
            {
                await Task.Delay(1000).ConfigureAwait(false);
                foreach (KeyValuePair<MessageId, object> record in _requestRegistry)
                {
                    if (record.Value is IClientCompletionSource source && source.TrySetTimeout(_options.RequestTimeout))
                    {
                        _requestRegistry.TryRemove(record.Key, out _);
                    }
                }
            }
        }

        /// <summary>
        /// Do not use this method if you don't need a response. In such a case use  <see cref="NotifyAsync{TNotification}" /> method.
        /// </summary>
        /// <param name="request"></param>
        /// <typeparam name="TRequest"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <returns></returns>
        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            MessageId id = _channel.Post(request);
            object result = _requestRegistry.GetOrAdd(id, msgId => new SolarWindCompletionSource<TResponse>());
            //It's highly unlikely but possible that response will come before adding completion source in the dictionary
            // In such a case return the value immediately
            if (result is TResponse response)
            {
                _requestRegistry.TryRemove(id, out _); //Simply remove the value from the dictionary
                return new ValueTask<TResponse>(response);
            }

            var completionSource = (TaskCompletionSource<TResponse>)result;
            return new ValueTask<TResponse>(completionSource.Task);
        }

        public void NotifyAsync<TNotification>(TNotification notification) => _channel.Post(notification);

        private void OnSolarWindCallback(Channel channel, in MessageHeader header, object data)
        {
            if (header.ReplyTo.IsEmpty)
            {
                InvokeCallback(data);
                return;
            }

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

        private void InvokeCallback(object data) => _eventCallback?.Invoke(data);

        public void Dispose() => _disposed = true;
    }
}