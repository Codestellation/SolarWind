using System;
using System.Collections.Concurrent;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public class SolarWindServer
    {
        private readonly Channel _channel;
        private readonly SolarWindServerCallback _callback;

        public SolarWindServer(Channel channel, SolarWindServerCallback callback)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _channel.SetCallback(OnCallback);
        }

        public MessageId Reply(object data, MessageId replyTo) => _channel.Post(data, replyTo);

        public MessageId Send(object data) => _channel.Post(data);

        private void OnCallback(Channel channel,in  MessageHeader header, object data) => _callback(header.MessageId, data);
    }
}