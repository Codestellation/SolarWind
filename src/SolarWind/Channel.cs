using System;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Channel is an abstraction over a connection between two hosts. It provides duplex asynchronous messages stream.
    /// </summary>
    public class Channel
    {
        private Connection _connection;
        private readonly ChannelOptions _options;

        private Task _reader;
        private Task _writer;
        private CancellationTokenSource _cancellationSource;
        private readonly Session _session;
        private SolarWindCallback _callback;


        public HubId RemoteHubId { get; internal set; }

        public Channel(ChannelOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _session = new Session(options.Serializer, OnIncomingMessage);
        }

        internal void OnReconnect(Connection connection)
        {
            Stop();
            _cancellationSource = new CancellationTokenSource();
            _connection = connection;
            _reader = StartReadingTask();
            _writer = StartWritingTask();
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
            while (!_cancellationSource.IsCancellationRequested)
            {
                await Receive().ConfigureAwait(false);
            }

            _connection.Close();
        }

        private async Task Receive()
        {
            PooledMemoryStream buffer = PooledMemoryStream.Rent();
            Message message;
            try
            {
                await Receive(buffer, WireHeader.Size).ConfigureAwait(false);

                WireHeader wireHeader = WireHeader.ReadFrom(buffer);
                buffer.CompleteRead();
                buffer.Reset();

                await Receive(buffer, wireHeader.PayloadSize.Value).ConfigureAwait(false);
                message = new Message(wireHeader.MessageHeader, buffer);
            }
            catch (Exception e)
            {
                if (!(e is OperationCanceledException))
                {
                    Console.WriteLine(e);
                }

                PooledMemoryStream.ResetAndReturn(buffer);
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
            catch (Exception e)
            {
                //TODO: Log exceptions
                Console.WriteLine(e);
            }
        }

        private ValueTask Receive(PooledMemoryStream buffer, int count) => _connection.Receive(buffer, count, _cancellationSource.Token);

        private async Task StartWritingTask()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    Message message = await _session
                        .DequeueAsync(_cancellationSource.Token)
                        .ConfigureAwait(false);

                    using (message)
                    {
                        await _connection
                            .WriteAsync(message, _cancellationSource.Token)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
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
            //writer must be stopped before stopping reader because reader is responsible for closing socket.  
            _writer.Wait();
            _reader.Wait();
        }
    }
}