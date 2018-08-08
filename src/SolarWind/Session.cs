using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind
{
    //TODO: When channel is being disposed dispose all messages in all queues to return streams to pool
    public class Session : IDisposable
    {
        private readonly Channel _channel;
        private readonly AwaitableQueue<Message> _outgoingQueue;
        private readonly Dictionary<MessageId, Message> _sent;

        private MessageId _currentMessageId;
        private readonly AwaitableQueue<Message> _incomingQueue;

        private readonly CancellationTokenSource _disposal;
        private readonly AwaitableQueue<(MessageHeader, object data)> _serializationQueue;

        private readonly Task _serialization;
        private readonly Task _deserialization;

        public Session(Channel channel)
        {
            _channel = channel;
            _serializationQueue = new AwaitableQueue<(MessageHeader, object data)>();
            _outgoingQueue = new AwaitableQueue<Message>();
            _incomingQueue = new AwaitableQueue<Message>();
            _disposal = new CancellationTokenSource();

            //Keep some sent messages until ACK received to be able to resend them in case of failure
            _sent = new Dictionary<MessageId, Message>();

            _serialization = StartSerializationTask();
            _deserialization = StartDeserializationTask();
        }

        private async Task StartSerializationTask()
        {
            while (!_disposal.IsCancellationRequested)
            {
                (MessageHeader header, object data) = await _serializationQueue.Await(_disposal.Token).ConfigureAwait(false);
                PooledMemoryStream payload = PooledMemoryStream.Rent();
                try
                {
                    _channel.Options.Serializer.Serialize(data, payload);
                    payload.CompleteWrite();
                    var message = new Message(header, payload);
                    _outgoingQueue.Enqueue(message);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    //TODO: Better logging
                    Console.WriteLine(e);
                }
            }
        }

        private async Task StartDeserializationTask()
        {
            //TODO: Handle exception to avoid exiting deserialization thread
            //TODO: Implement graceful shutdown
            while (!_disposal.IsCancellationRequested)
            {
                object data;
                Message incoming;
                using (incoming = await _incomingQueue.Await(_disposal.Token).ConfigureAwait(false))
                {
                    data = _channel.Options.Serializer.Deserialize(incoming.Header.TypeId, incoming.Payload);
                }

                try
                {
                    _channel.Options.Callback(_channel, incoming.Header, data);
                }
                catch (Exception e)
                {
                    //TODO: Use better logging
                    Console.WriteLine(e);
                }
            }
        }


        public void EnqueueIncoming(in Message message) => _incomingQueue.Enqueue(message);

        public MessageId EnqueueOutgoing(MessageTypeId typeId, object data)
        {
            MessageId id;
            lock (_outgoingQueue)
            {
                id = _currentMessageId = _currentMessageId.Next();
            }

            var header = new MessageHeader(typeId, id);
            _serializationQueue.Enqueue((header, data));
            return id;
        }

        public void Ack(MessageId messageId)
        {
            lock (_sent)
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

        public void Dispose()
        {
            //TODO: Think how to dispose it in a correct manner
        }
    }
}