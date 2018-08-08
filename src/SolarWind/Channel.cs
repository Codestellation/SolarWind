using System;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

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

        private Task StartReadingTask() => Task.Run(() =>
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                Receive();
            }

            _connection.Close();
        });

        private void Receive()
        {
            PooledMemoryStream buffer = PooledMemoryStream.Rent();
            if (!Receive(buffer, WireHeader.Size))
            {
                PooledMemoryStream.ResetAndReturn(buffer);
                return;
            }

            WireHeader wireHeader = WireHeader.ReadFrom(buffer);
            buffer.CompleteRead();
            buffer.Reset();

            if (!Receive(buffer, wireHeader.PayloadSize.Value))
            {
                PooledMemoryStream.ResetAndReturn(buffer);
                return;
            }

            var message = new Message(wireHeader.MessageHeader, buffer);

            _session.EnqueueIncoming(message);
        }

        private bool Receive(PooledMemoryStream buffer, int count) => _connection.Receive(buffer, count, _cancellationSource.Token);

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

                    _connection.Write(message);
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