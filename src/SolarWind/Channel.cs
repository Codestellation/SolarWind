using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Channel is an abstraction over a connection between two hosts. It provides duplex asynchronous messages stream.
    /// </summary>
    public class Channel
    {
        private Connection _connection;
        private readonly ChannelOptions _options;

        private CancellationTokenSource _cancellationSource;
        private readonly Session _session;
        private SolarWindCallback _callback;
        private readonly ILogger<Channel> _logger;
        private readonly Message[] _batch;
        private int _batchLength;

        public HubId RemoteHubId { get; internal set; }

        internal ChannelId ChannelId { get; set; }
        internal Uri RemoteUri { get; set; }

        public Channel(ChannelOptions options, ILoggerFactory factory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _batch = new Message[100];
            _logger = factory.CreateLogger<Channel>();
            _session = new Session(options.Serializer, OnIncomingMessage, factory.CreateLogger<Session>());
        }

        internal void OnReconnect(Connection connection)
        {
            _logger.LogInformation($"Reconnected to {RemoteHubId}");
            Stop(false);

            _cancellationSource = new CancellationTokenSource();
            _connection = connection;

            Task.Run(StartReadingTask);
            Task.Run(StartWritingTask);
        }

        /// <summary>
        /// Puts a message into send queue and returns assigned message identifier.
        /// </summary>
        /// <param name="data">The message</param>
        /// <param name="replyTo">An id of the message which replies to</param>
        /// <returns>An assigned id to the outgoing message</returns>
        public MessageId Post(object data, MessageId replyTo = default)
            => _session.EnqueueOutgoing(data, replyTo);

        /// <summary>
        /// Sets callback for the channel. Previous callback will be replaced with the supplied one.
        /// </summary>
        /// <param name="callback"></param>
        public void SetCallback(SolarWindCallback callback)
            => _callback = callback ?? throw new ArgumentNullException(nameof(callback));

        private async Task StartReadingTask()
        {
            _logger.LogInformation($"Starting receiving from {RemoteHubId.Id}");
            CancellationTokenSource cancellation = _cancellationSource;
            while (!cancellation?.IsCancellationRequested ?? true)
            {
                await Receive().ConfigureAwait(false);
            }
        }

        private async ValueTask Receive()
        {
            var buffer = new PooledMemoryStream();
            Message message;
            try
            {
                await Receive(buffer, WireHeader.Size).ConfigureAwait(false);
                buffer.Position = 0;
                WireHeader wireHeader = WireHeader.ReadFrom(buffer);
                buffer.Reset();

                await Receive(buffer, wireHeader.PayloadSize.Value).ConfigureAwait(false);
                buffer.Position = 0;
                message = new Message(wireHeader.MessageHeader, buffer);

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"Received {message.Header.ToString()}");
                }
            }
            catch (IOException ex)
            {
                _logger.LogInformation(ex, "Receive Error.");
                Stop(true);
                buffer.Dispose();
                return;
            }
            catch (OperationCanceledException)
            {
                buffer.Dispose();
                return;
            }
            catch (Exception ex)
            {
                buffer.Dispose();
                _logger.LogError(ex, "Receive error");
                return;
            }

            _session.EnqueueIncoming(message);
        }

        private void OnIncomingMessage(in MessageHeader header, object data)
        {
            try
            {
                (_callback ?? _options.Callback).Invoke(this, header, data);
            }
            catch (Exception ex)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(ex, $"Error during callback. {header.ToString()}");
                }
            }
        }

        private ValueTask Receive(PooledMemoryStream buffer, int count) => _connection.ReceiveAsync(buffer, count, _cancellationSource.Token);

        private async Task StartWritingTask()
        {
            _logger.LogInformation($"Starting writing to {RemoteHubId.Id}");

            CancellationTokenSource cancellationTokenSource = _cancellationSource;

            while (!cancellationTokenSource?.IsCancellationRequested ?? true)
            {
                try
                {
                    _logger.LogDebug($"Dequeuing batch to send to {RemoteHubId.ToString()}");
                    while (!cancellationTokenSource.IsCancellationRequested
                           && _batchLength == 0 //Batch was not send due to exception. will try to resend it
                           && (_batchLength = _session.TryDequeueBatch(_batch)) == 0)
                    {
                        await _session.AwaitOutgoing(cancellationTokenSource.Token).ConfigureAwait(false);
                    }

                    _logger.LogDebug($"Dequeued {_batchLength} messages to {RemoteHubId.ToString()}");
                    await TrySendBatch(cancellationTokenSource).ConfigureAwait(false);

                    Array.ForEach(_batch, m => m.Dispose());
                    Array.Clear(_batch, 0, _batch.Length); //Allow GC to collect streams
                    _batchLength = 0;
                }
                //It's my buggy realization. I have to enclose socket exception into IOException as other streams do.
                catch (IOException ex)
                {
                    _logger.LogDebug(ex, "Send error");
                    Stop(true);

                    break;
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Send worker stopped");
                    break;
                }
            }
        }

        private async Task TrySendBatch(CancellationTokenSource cancellationTokenSource)
        {
            for (var i = 0; i < _batchLength; i++)
            {
                //_logger.LogDebug($"Writing message {message.Header.ToString()}");
                await _connection
                    .WriteAsync(_batch[i], cancellationTokenSource.Token)
                    .ConfigureAwait(false);
            }

            await _connection
                .FlushAsync(cancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        //It's made internal to avoid occasional calls from user's code.
        internal void Dispose()
        {
            Stop(false);
            _session.Dispose();
        }

        private void Stop(bool reconnect)
        {
            CancellationTokenSource source = Interlocked.Exchange(ref _cancellationSource, null);

            if (source == null)
            {
                return;
            }

            source.Cancel();
            _connection.Dispose();

            if (reconnect)
            {
                _connection.Reconnect();
            }
        }
    }
}