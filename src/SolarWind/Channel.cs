using System;
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

        private Thread _reader;
        private Thread _writer;
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
            _reader = new Thread(StartReadingTask);
            _reader.Start();

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

        private void StartReadingTask()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                Receive();
            }

            _connection.Close();
        }

        private void Receive()
        {
            var buffer = new PooledMemoryStream();
            Message message;
            try
            {
                Receive(buffer, WireHeader.Size);
                buffer.Position = 0;
                WireHeader wireHeader = WireHeader.ReadFrom(buffer);
                buffer.Reset();

                Receive(buffer, wireHeader.PayloadSize.Value);
                buffer.Position = 0;
                message = new Message(wireHeader.MessageHeader, buffer);
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                {
                    //if (_logger.IsEnabled(LogLevel.Error))
                    //{
                    //    _logger.LogError(ex, "Error during receive");
                    //}
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

        private void Receive(PooledMemoryStream buffer, int count) => _connection.Receive(buffer, count);

        private async Task StartWritingTask()
        {
            //var batch = new Message[100];
            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    //Try to dequeue a batch and send and flush after that
                    //var batchSize = _session.TryDequeueBatch(batch);

                    if (_session.TryDequeueAsync(out Message message))
                    {
                        //_logger.LogDebug($"Writing message {message.Header.ToString()}");

                        using (message)
                        {
                            string msg = message.Header.ToString();
                            Console.Write($"Writing {msg}. ");
                            await _connection
                                .WriteAsync(message, _cancellationSource.Token)
                                .ConfigureAwait(false);
                            Console.Write("Written. ");
                            await _connection
                                .FlushAsync(_cancellationSource.Token)
                                .ConfigureAwait(false);
                            Console.WriteLine("Flushed.");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    //Debugger.Break();
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
            //writer must be stopped before stopping reader because reader is responsible for closing socket.  
            //_writer.Wait();
            //_reader.Wait();
        }
    }
}