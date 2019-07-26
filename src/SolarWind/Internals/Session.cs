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

        private readonly ILogger _logger;

        private readonly PreemptiveHashSet<MessageId> _processedMessages;

        public Session(ISerializer serializer, DeserializationCallback callback, ILogger logger)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            //Use thread pool to avoid Enqueue caller thread to start serializing all incoming messages.
            _serializationQueue = new AwaitableQueue<(MessageId id, MessageId replyTo, object data)>(ContinuationOptions.ContinueAsync);
            _outgoingQueue = new AwaitableQueue<Message>(ContinuationOptions.ContinueAsync);

            _processedMessages = new PreemptiveHashSet<MessageId>(256 * 1024);

            //It's possible that poller thread will reach this queue and perform then continuation on the queue, and the following
            // message processing as well and thus stop reading all the sockets.
            _incomingQueue = new AwaitableQueue<Message>(ContinuationOptions.ContinueAsync);
            _disposal = new CancellationTokenSource();

            _currentMessageId = MessageId.Initialize();

            Task
                .Factory
                .StartNew(StartSerializationTask, CancellationToken.None, TaskCreationOptions.None, IOTaskScheduler.Instance);
            Task
                .Factory
                .StartNew(StartDeserializationTask, CancellationToken.None, TaskCreationOptions.None, IOTaskScheduler.Instance);
        }

        private async Task StartSerializationTask()
        {
            var batch = new (MessageId id, MessageId replyTo, object data)[10];

            while (!_disposal.IsCancellationRequested)
            {
                var batchLength = _serializationQueue.TryDequeueBatch(batch);

                if (batchLength == 0)
                {
                    await _serializationQueue.AwaitEnqueued(_disposal.Token).ConfigureAwait(ContinueOn.IOScheduler);
                    continue;
                }

                for (var i = 0; i < batchLength; i++)
                {
                    var payload = new PooledMemoryStream();
                    try
                    {
                        (MessageId id, MessageId replyTo, object data) = batch[i];
                        MessageTypeId typeId = _serializer.Serialize(data, payload);
                        var header = new MessageHeader(typeId, id, replyTo);
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
                        //TODO: Put a message into incoming queue to notify about serialization failure
                        payload.Dispose();
                        if (_logger.IsEnabled(LogLevel.Error))
                        {
                            _logger.LogError(ex, "Serialization failure.");
                        }
                    }
                }

                Array.Clear(batch, 0, batchLength); //free incoming object
            }
        }

        private async Task StartDeserializationTask()
        {
            //TODO: Implement graceful shutdown

            var batch = new Message[10];


            while (!_disposal.IsCancellationRequested)
            {
                var batchLength = _incomingQueue.TryDequeueBatch(batch);

                if (batchLength == 0)
                {
                    await _incomingQueue.AwaitEnqueued(_disposal.Token).ConfigureAwait(ContinueOn.IOScheduler);
                    continue;
                }

                for (var i = 0; i < batchLength; i++)
                {
                    Message incoming = batch[i];
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
        }


        public void EnqueueIncoming(in Message message)
        {
            //Prevents double processing of the message
            if (_processedMessages.Add(message.Header.MessageId))
            {
                _incomingQueue.Enqueue(message);
            }
            else
            {
                _logger.LogDebug($"Message {message.Header} already processed");
                //Release buffers
                message.Dispose();
            }
        }

        public MessageId EnqueueOutgoing(object data, MessageId replyTo)
        {
            MessageId id;
            lock (_outgoingQueue)
            {
                id = _currentMessageId = _currentMessageId.Next();
            }

            _serializationQueue.Enqueue((id, replyTo, data));
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

        private void LogAndFail(Task task)
        {
            _logger.LogCritical(task.Exception, "Task failed.");
            Environment.FailFast($"Task failed: {task.Exception}", task.Exception);
        }
    }
}