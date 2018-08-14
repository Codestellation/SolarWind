using System;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public class Channel : IDisposable
    {
        private Connection _connection;
        internal readonly SolarWindHubOptions Options;

        private Task _reader;
        private Task _writer;
        private CancellationTokenSource _cancellationSource;
        private readonly Session _session;

        internal Connection Connection => _connection;

        public Channel(SolarWindHubOptions options)
        {
            Options = options;
            _session = new Session(this);
        }

        public void OnReconnect(Connection connection)
        {
            Stop();
            _cancellationSource = new CancellationTokenSource();
            _connection = connection;
            _reader = StartReadingTask();
            _writer = StartWritingTask();
        }

        public MessageId Post(MessageTypeId typeId, object data) => _session.EnqueueOutgoing(typeId, data);

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
            //TODO: Error handling routines
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

        private Task Receive(PooledMemoryStream buffer, int count) => _connection.Receive(buffer, count, _cancellationSource.Token);

        private async Task StartWritingTask()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    //TODO: Consider sync way to get result
                    Message message = await _session
                        .DequeueAsync(_cancellationSource.Token)
                        .ConfigureAwait(false);

                    using (message)
                    {
                        await Connection
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

        public void Dispose()
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