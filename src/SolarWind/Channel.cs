using System;
using System.Net.Sockets;
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

        public HubId RemoteHubId { get; internal set; }

        public Channel(ChannelOptions options, ILoggerFactory factory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _logger = factory.CreateLogger<Channel>();
            _session = new Session(options.Serializer, OnIncomingMessage, factory.CreateLogger<Session>());
        }

        internal void OnReconnect(Connection connection)
        {
            Stop();
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
            CancellationTokenSource cancellation = _cancellationSource;
            while (!cancellation.IsCancellationRequested)
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
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        _logger.LogError(ex, "Receive error");
                    }
                }
                //if(ex is SocketException sex && sex.ErrorCode == SocketError.TimedOut)


                buffer.Dispose();
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
            var batch = new Message[100];
            CancellationTokenSource cancellationTokenSource = _cancellationSource;

            while (!cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    var batchLength = _session.TryDequeueBatch(batch);

                    if (batchLength == 0)
                    {
                        await _session.AwaitOutgoing(cancellationTokenSource.Token).ConfigureAwait(false);
                        continue;
                    }

                    //TODO: Dispose messages in case of exception
                    for (var i = 0; i < batchLength; i++)
                    {
                        //_logger.LogDebug($"Writing message {message.Header.ToString()}");
                            await _connection
                                .WriteAsync(batch[i], cancellationTokenSource.Token)
                                .ConfigureAwait(false);
                        
                    }

                    Array.Clear(batch, 0, batch.Length); //Allow GC to collect streams

                    await _connection
                        .FlushAsync(cancellationTokenSource.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    if (ex.ErrorCode == (int)SocketError.ConnectionReset)
                    {
                        Stop();
                        _connection.Reconnect();
                    }

                    break;
                }
            }
        }

        //It's made internal to avoid occasional calls from user's code. 
        internal void Dispose()
        {
            Stop();
            _session.Dispose();
        }

        private void Stop()
        {
            if (_cancellationSource == null)
            {
                return;
            }

            _cancellationSource.Cancel();
            _connection.Dispose();
        }
    }
}