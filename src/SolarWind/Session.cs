using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind
{
    //TODO: When channel is being disposed dispose all messages in all queues to return streams to pool `
    public class Session : IDisposable
    {
        private readonly Channel _channel;
        private readonly AwaitableQueue<Message> _outgoingQueue;
        private readonly Dictionary<MessageId, Message> _sent;

        private MessageId _currentMessageId;
        private readonly AwaitableQueue<Message> _incomingQueue;
        private readonly Task _deserializationTask;

        public Session(Channel channel)
        {
            _channel = channel;
            _outgoingQueue = new AwaitableQueue<Message>();
            _incomingQueue = new AwaitableQueue<Message>();

            //Keep some sent messages until ACK received to be able to resend them in case of failure
            _sent = new Dictionary<MessageId, Message>();

            _deserializationTask = StartDeserializationTask();
        }

        private Task StartDeserializationTask() => Task.Run((Action)DeserializeAndCallback);

        private async void DeserializeAndCallback()
        {
            //TODO: Handle exception to avoid exiting deserialization thread
            //TODO: Implement graceful shutdown
            while (true)
            {
                object data;
                Message incoming;
                using (incoming = await _incomingQueue.Await(CancellationToken.None).ConfigureAwait(false))
                {
                    data = _channel.Options.Serializer.Deserialize(incoming.Header.TypeId, incoming.Payload);
                }

                _channel.Options.Callback(_channel, incoming.Header, data);
            }
        }


        public void EnqueueIncoming(in Message message)
        {
            lock (_incomingQueue)
            {
                _incomingQueue.Enqueue(message);
                Monitor.Pulse(_incomingQueue);
            }
        }

        public MessageId EnqueueOutgoing(MessageTypeId typeId, PooledMemoryStream payload)
        {
            MessageId id;
            lock (_outgoingQueue)
            {
                id = _currentMessageId = _currentMessageId.Next();
            }

            var header = new MessageHeader(typeId, id);
            var message = new Message(header, payload);
            _outgoingQueue.Enqueue(message);

            return id;
        }

        public void Ack(MessageId messageId)
        {
            lock (_outgoingQueue)
            {
                if (_sent.TryGetValue(messageId, out Message message))
                {
                    message.Dispose();
                    _sent.Remove(messageId);
                }
            }
        }

        public async ValueTask<Message> DequeueAsync(CancellationToken cancellation)
        {
            Message result = await _outgoingQueue.Await(cancellation);
            lock (_sent)
            {
                _sent.Add(result.Header.MessageId, result);
            }

            return result;
        }

        //public void Cancel() => Volatile.Read(ref _completionSource)?.TrySetCanceled();
        public void Cancel()
        {
        }

        public void Dispose()
        {
            //TODO: Dispose messages in queues.

            _channel?.Dispose();
            _deserializationTask?.Dispose();
        }
    }
}