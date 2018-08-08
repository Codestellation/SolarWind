using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind
{
    //TODO: When channel is being disposed dispose all messages in all queues to return streams to pool `
    public class Session
    {
        private readonly Channel _channel;
        private readonly Queue<Message> _outgoingQueue;
        private readonly Dictionary<MessageId, Message> _sent;

        private MessageId _currentMessageId;
        private TaskCompletionSource<Message> _completionSource;
        private readonly Queue<Message> _incomingQueue;
        private Task _deserializationTask;

        public Session(Channel channel)
        {
            _channel = channel;
            _outgoingQueue = new Queue<Message>();
            //Keep some sent messages until ACK received to be able to resend them in case of failure
            _sent = new Dictionary<MessageId, Message>();
            _incomingQueue = new Queue<Message>();
            _deserializationTask = StartDeserializationTask();
        }

        private Task StartDeserializationTask() => Task.Run((Action)DeserializeAndCallback);

        private void DeserializeAndCallback()
        {
            //TODO: Handle exception to avoid exiting deserialization thread
            while (true)
            {
                object data;
                Message incoming;
                using (incoming = DequeueIncoming())
                {
                    data = _channel.Options.Serializer.Deserialize(incoming.Header.TypeId, incoming.Payload);
                }

                _channel.Options.Callback(_channel, incoming.Header, data);
            }
        }

        private Message DequeueIncoming()
        {
            lock (_incomingQueue)
            {
                if (_incomingQueue.Count == 0)
                {
                    Monitor.Wait(_incomingQueue);
                }
                else
                {
                    return _incomingQueue.Dequeue();
                }
            }

            lock (_incomingQueue)
            {
                return _incomingQueue.Dequeue();
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
            lock (_outgoingQueue)
            {
                _currentMessageId = _currentMessageId.Next();
                var header = new MessageHeader(typeId, _currentMessageId);
                var message = new Message(header, payload);
                if (_completionSource == null)
                {
                    _outgoingQueue.Enqueue(message);
                }
                else
                {
                    _sent.Add(_currentMessageId, message);
                    _completionSource.SetResult(message);
                    _completionSource = null;
                }

                return _currentMessageId;
            }
        }

        public void Ack(MessageId messageId)
        {
            lock (_outgoingQueue)
            {
                _sent.Remove(messageId);
            }
        }

        public bool TryDequeueSync(out Message message)
        {
            if (Monitor.TryEnter(_outgoingQueue))
            {
                if (_outgoingQueue.Count > 0)
                {
                    message = _outgoingQueue.Dequeue();
                    _sent.Add(message.Header.MessageId, message);
                    return true;
                }

                Monitor.Exit(_outgoingQueue);
            }

            message = default;
            return false;
        }

        //TODO: Replace with ValueTask<> with usage of IValueTaskSource (don't have to have pool, it's supposed to be used be the only reader)

        public Task<Message> DequeueAsync(CancellationToken token)
        {
            //TODO: Apply token
            lock (_outgoingQueue)
            {
                if (_outgoingQueue.Count > 0)
                {
                    Message message = _outgoingQueue.Dequeue();
                    _sent.Add(message.Header.MessageId, message);
                    return Task.FromResult(message);
                }

                _completionSource = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _completionSource.Task;
            }
        }

        public void Cancel() => Volatile.Read(ref _completionSource)?.TrySetCanceled();
    }
}