using System;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Codestellation.SolarWind.Threading;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    //TODO: When channel is being disposed dispose all messages in all queues to return streams to pool
    internal class Session : IDisposable
    {
        //Supposed to be used with the session class only
        public delegate void DeserializationCallback(in MessageHeader header, object data);

        private readonly ISerializer _serializer;
        private readonly DeserializationCallback _callback;
        private readonly AwaitableQueue<Message> _outgoingQueue;

        private MessageId _currentMessageId;
        private readonly AwaitableQueue<Message> _incomingQueue;

        private readonly CancellationTokenSource _disposal;
        private readonly AwaitableQueue<(MessageId id, MessageId replyTo, object data)> _serializationQueue;

        private readonly Task _serialization;
        private readonly Task _deserialization;
        private readonly ILogger _logger;

        public Session(ISerializer serializer, DeserializationCallback callback, ILogger logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //Use thread pool to avoid Enqueue caller thread to start serializing all incoming messages. 
            _serializationQueue = new AwaitableQueue<(MessageId id, MessageId replyTo, object data)>(ContinuationOptions.ForceDefaultTaskScheduler);
            _outgoingQueue = new AwaitableQueue<Message>();

            //It's possible that poller thread will reach this queue and perform then continuation on the queue, and the following
            // message processing as well and thus stop reading all the sockets. 
            _incomingQueue = new AwaitableQueue<Message>();
            _disposal = new CancellationTokenSource();

            _serialization = StartSerializationTask();
            _deserialization = StartDeserializationTask();
        }

        private async Task StartSerializationTask()
        {
            while (!_disposal.IsCancellationRequested)
            {
                (MessageId id, MessageId replyTo, object data) = await _serializationQueue
                    .Await(_disposal.Token)
                    .ConfigureAwait(false);

                PooledMemoryStream payload = PooledMemoryStream.Rent();
                try
                {
                    MessageTypeId typeId = _serializer.Serialize(data, payload);
                    payload.CompleteWrite();
                    var header = new MessageHeader(typeId, id, replyTo);
                    var message = new Message(header, payload);
                    _outgoingQueue.Enqueue(message);
                    //if (_logger.IsEnabled(LogLevel.Debug))
                    //{
                    //    _logger.LogDebug($"Serialized msg {id.ToString()}");
                    //}
                }
                catch (OperationCanceledException)
                {
                    PooledMemoryStream.ResetAndReturn(payload);
                    break;
                }
                catch (Exception ex)
                {
                    PooledMemoryStream.ResetAndReturn(payload);
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Serialization failure.");
                    }
                }
            }
        }

        private async Task StartDeserializationTask()
        {
            //TODO: Handle exception to avoid exiting deserialization thread
            //TODO: Implement graceful shutdown
            while (!_disposal.IsCancellationRequested)
            {
                Message incoming = default;
                try
                {
                    incoming = await _incomingQueue.Await(_disposal.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Failed to dequeue a message");
                    }

                    continue;
                }

                object data;
                try
                {
                    data = _serializer.Deserialize(incoming.Header, incoming.Payload);
                    incoming.Dispose();
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, $"Deserialization failure. {incoming.Header.ToString()}");
                    }

                    incoming.Dispose();
                    continue;
                }

                try
                {
                    _callback(incoming.Header, data);
                }
                catch (Exception ex)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, $"Failure during callback. {incoming.Header.ToString()}");
                    }
                }
            }
        }


        public void EnqueueIncoming(in Message message) => _incomingQueue.Enqueue(message);

        public MessageId EnqueueOutgoing(object data, MessageId replyTo)
        {
            MessageId id;
            lock (_outgoingQueue)
            {
                id = _currentMessageId = _currentMessageId.Next();
            }

            _serializationQueue.Enqueue((id, replyTo, data));
            //if (_logger.IsEnabled(LogLevel.Debug))
            //{
            //    _logger.LogDebug($"Enqueued msg {id.ToString()}");
            //}
            return id;
        }

        public ValueTask<Message> DequeueAsync(CancellationToken cancellation) => _outgoingQueue.Await(cancellation);

        public void Dispose()
        {
            _incomingQueue.Dispose(m => m.Dispose());
            _outgoingQueue.Dispose(m => m.Dispose());
            _serializationQueue.Dispose(m => { });
        }
    }
}