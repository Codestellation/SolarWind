using System;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public delegate ValueTask<object> AsyncMessageProcessor(object data);

    public class SolarAsyncWindServer
    {
        private readonly Channel _channel;
        private readonly AsyncMessageProcessor _processor;

        public SolarAsyncWindServer(Channel channel, AsyncMessageProcessor callback)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _processor = callback ?? throw new ArgumentNullException(nameof(callback));
            _channel.SetCallback(OnCallback);
        }

        public MessageId Send(object data) => _channel.Post(data);

        private void OnCallback(Channel channel, in MessageHeader header, object data)
        {
            ValueTask<object> task = _processor(data);

            if (task.IsCompletedSuccessfully)
            {
                _channel.Post(task.Result, header.MessageId);
                return;
            }

            RunAsync(task, header.MessageId);
        }

        private async void RunAsync(ValueTask<object> task, MessageId replyTo)
        {
            object result = await task.ConfigureAwait(false);
            _channel.Post(result, replyTo);
        }
    }
}