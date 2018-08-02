using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind
{
    public class Channel : IDisposable
    {
        private Connection _connection;
        private readonly SolarWindHubOptions _options;

        private Task _reader;
        private Task _writer;
        private readonly MemoryStream _writeBuffer;
        private readonly MemoryStream _readBuffer;
        private CancellationTokenSource _cancellationSource;
        private readonly Session _session;

        public Channel(SolarWindHubOptions options)
        {
            _options = options;
            _session = new Session();

            _writeBuffer = new MemoryStream(1024);
            _readBuffer = new MemoryStream(1024);
        }

        public void OnReconnect(Connection connection)
        {
            Stop();
            _cancellationSource = new CancellationTokenSource();
            _connection = connection;
            _reader = StartReadingTask();
            _writer = StartWritingTask();
        }

        public MessageId Post(Message message) => _session.Enqueue(message);

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
            if (!Receive(Header.Size))
            {
                return;
            }

            Header header = Header.ReadFrom(_readBuffer.GetBuffer());

            if (!Receive(header.PayloadSize.Value))
            {
                return;
            }

            object payload = _options.Serializer.Deserialize(header.MessageTypeId, _connection.Stream);
            var message = new Message(header.MessageTypeId, payload);
            _options.Callback(this, message);
        }

        private bool Receive(int count) => _connection.Receive(_readBuffer, count, _cancellationSource.Token);

        private async Task StartWritingTask()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                //TODO: Make a synchronous path for cases where message is already available without usage of ValueTask (it's a huge structure)
                ValueTask<(MessageId, Message)> valueTask = _session.Dequeue(_cancellationSource.Token);

                try
                {
                    (MessageId messageId, Message message) = valueTask.IsCompletedSuccessfully
                        ? valueTask.Result
                        : await valueTask.ConfigureAwait(false);

                    _options.Serializer.SerializeMessage(_writeBuffer, in message, messageId);
                    _connection.Write(_writeBuffer.GetBuffer(), 0, (int)_writeBuffer.Position);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        public void Dispose() => Stop();

        private void Stop()
        {
            if (_cancellationSource == null)
            {
                return;
            }

            _cancellationSource.Cancel();
            //writer must be stopped before reader because reader is responsible for closing socket.  
            _writer.Wait();
            _reader.Wait();
        }
    }
}