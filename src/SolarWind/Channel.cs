using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind
{
    public class Channel : IDisposable
    {
        private readonly Socket _socket;
        private readonly Stream _tcpStream;
        private readonly SolarWindHubOptions _options;

        private readonly Task _reader;
        private readonly Task _writer;
        private readonly MemoryStream _writeBuffer;
        private readonly MemoryStream _readBuffer;
        private readonly CancellationTokenSource _cancellationSource;
        private readonly Session _session;


        public static Channel Server(Socket socket, Stream networkStream, SolarWindHubOptions options) => new Channel(socket, networkStream, options);

        public static Channel Client(Socket socket, Stream networkStream, SolarWindHubOptions options) => new Channel(socket, networkStream, options);

        private Channel(Socket socket, Stream tcpStream, SolarWindHubOptions options)
        {
            _socket = socket;
            _tcpStream = tcpStream;
            _options = options;


            _session = new Session();
            _cancellationSource = new CancellationTokenSource();

            _writeBuffer = new MemoryStream(1024);
            _readBuffer = new MemoryStream(1024);

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

            _socket.Close();
            _socket.Dispose();
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

            object payload = _options.Serializer.Deserialize(header.MessageTypeId, _tcpStream);
            var message = new Message(header.MessageTypeId, payload);
            _options.Callback(this, message);
        }

        private bool Receive(int bytesToReceive)
        {
            if (_readBuffer.Capacity < bytesToReceive)
            {
                _readBuffer.Capacity = bytesToReceive;
            }

            do
            {
                //TODO: Anylyze how socket error works. 
                if (_cancellationSource.IsCancellationRequested || _socket.Poll(100_000, SelectMode.SelectError))
                {
                    //TODO: Find a better way to 
                    return false;
                }

                if (!_socket.Poll(100_000, SelectMode.SelectRead))
                {
                    continue;
                }

                byte[] buffer = _readBuffer.GetBuffer();

                var position = (int)_readBuffer.Position;
                int count = bytesToReceive - position;

                _readBuffer.Position += _tcpStream.Read(buffer, position, count);
            } while (_readBuffer.Position < bytesToReceive);

            _readBuffer.Position = 0;
            _readBuffer.SetLength(bytesToReceive);
            return true;
        }

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
                    _tcpStream.Write(_writeBuffer.GetBuffer(), 0, (int)_writeBuffer.Position);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            _cancellationSource.Cancel();
            //writer must be stopped before reader because reader is responsible for closing socket.  
            _writer.Wait();
            _reader.Wait();
        }

        public void OnReconnect(Socket socket, Stream networkStream)
        {
            //TODO: replace socket and 
        }
    }
}