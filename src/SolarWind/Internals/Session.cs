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
        private readonly ArrayBlockingQueue<Message> _incomingQueue;

        private readonly CancellationTokenSource _disposal;
        private readonly ArrayBlockingQueue<(MessageId id, MessageId replyTo, object data)> _serializationQueue;

        private readonly ILogger _logger;
        private readonly Thread _serializationThread;
        private readonly Thread _deserializationThread;

        public Session(ISerializer serializer, DeserializationCallback callback, ILogger logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //Use thread pool to avoid Enqueue caller thread to start serializing all incoming messages. 
            _serializationQueue = new ArrayBlockingQueue<(MessageId id, MessageId replyTo, object data)>();
            _outgoingQueue = new AwaitableQueue<Message>(ContinuationOptions.ForceDefaultTaskScheduler);

            //It's possible that poller thread will reach this queue and perform then continuation on the queue, and the following
            // message processing as well and thus stop reading all the sockets. 
            _incomingQueue = new ArrayBlockingQueue<Message>();
            _disposal = new CancellationTokenSource();

            _serializationThread = new Thread(StartSerializationTask);
            _serializationThread.Start();

            _deserializationThread = new Thread(StartDeserializationTask);
            _deserializationThread.Start();
        }

        private void StartSerializationTask()
        {
            while (!_disposal.IsCancellationRequested)
            {
                if (!_serializationQueue.TryDequeue(out (MessageId id, MessageId replyTo, object data) tuple))
                {
                    continue;
                }

                var payload = new PooledMemoryStream();
                try
                {
                    MessageTypeId typeId = _serializer.Serialize(tuple.data, payload);
                    var header = new MessageHeader(typeId, tuple.id, tuple.replyTo);
                    var message = new Message(header, payload);
                    payload.Position = 0;
                    _outgoingQueue.Enqueue(message);
                }
                catch (OperationCanceledException)
                {
                    payload.Dispose();
                    break;
                }
                catch (Exception ex)
                {
                    payload.Dispose();
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Serialization failure.");
                    }
                }
            }
        }

        private void StartDeserializationTask()
        {
            //TODO: Handle exception to avoid exiting deserialization thread
            //TODO: Implement graceful shutdown
            while (!_disposal.IsCancellationRequested)
            {
                if (!_incomingQueue.TryDequeue(out Message incoming))
                {
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

        public int TryDequeueBatch(Message[] batch) => _outgoingQueue.TryDequeueBatch(batch);

        public ValueTask AwaitOutgoing(CancellationToken cancellation) => _outgoingQueue.AwaitEnqueued(cancellation);

        public void Dispose()
        {
            _disposal.Cancel();
            _incomingQueue.Dispose(m => m.Dispose());
            _outgoingQueue.Dispose(m => m.Dispose());
            _serializationQueue.Dispose(m => { });
        }
    }
}