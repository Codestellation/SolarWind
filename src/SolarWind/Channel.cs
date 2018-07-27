using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
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
        private readonly BlockingCollection<Message> _outgoings;
        private readonly MemoryStream _writeBuffer;
        private readonly MemoryStream _readBuffer;
        private readonly CancellationTokenSource _cancellationSource;


        public static Channel Server(Socket socket, SolarWindHubOptions options)
        {
            Stream networkStream = new NetworkStream(socket);
            if (options.Certificate != null)
            {
                //TODO: Try accomplish this async later
                var sslStream = new SslStream(networkStream);
                sslStream.AuthenticateAsServer(options.Certificate, false, SslProtocols.Tls12, true);
                networkStream = sslStream;
            }

            return new Channel(socket, networkStream, options);
        }

        public static Channel Client(Socket socket, SolarWindHubOptions options)
        {
            //TODO: How to connect using TLS?
            var stream = new NetworkStream(socket);
            return new Channel(socket, stream, options);
        }

        private Channel(Socket socket, Stream tcpStream, SolarWindHubOptions options)
        {
            _socket = socket;
            _tcpStream = tcpStream;
            _options = options;

            _cancellationSource = new CancellationTokenSource();
            _outgoings = new BlockingCollection<Message>();

            _writeBuffer = new MemoryStream(1024);
            _readBuffer = new MemoryStream(1024);

            _reader = StartReadingTask();
            _writer = StartWritingTask();
        }

        public void Post(Message message) => _outgoings.Add(message);

        private Task StartReadingTask() => Task.Run(() =>
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                try
                {
                    Receive();
                }
                catch (ObjectDisposedException) when (_cancellationSource.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
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

            var readList = new List<Socket>(1);
            var errorList = new List<Socket>(1);

            while (_readBuffer.Position < bytesToReceive)
            {
                readList.Add(_socket);
                errorList.Add(_socket);

                Socket.Select(readList, null, errorList, 100_000);

                if (_cancellationSource.IsCancellationRequested)
                {
                    return false;
                }

                if (readList.Count == 0)
                {
                    continue;
                }

                byte[] buffer = _readBuffer.GetBuffer();

                var position = (int)_readBuffer.Position;
                var count = bytesToReceive - position;

                _readBuffer.Position += _tcpStream.Read(buffer, position, count);
            }

            _readBuffer.Position = 0;
            _readBuffer.SetLength(bytesToReceive);
            return true;
        }

        private Task StartWritingTask() => Task.Run(() =>
        {
            try
            {
                foreach (Message message in _outgoings.GetConsumingEnumerable(_cancellationSource.Token))
                {
                    _options.Serializer.SerializeMessage(_writeBuffer, in message);
                    _tcpStream.Write(_writeBuffer.GetBuffer(), 0, (int)_writeBuffer.Position);
                }
            }
            catch (OperationCanceledException)
            {
            }
        });

        public void Dispose()
        {
            _cancellationSource.Cancel();
            _writer.Wait();
            _tcpStream.Dispose();
            _socket.Disconnect(false);
            _socket.Dispose();
            _reader.Wait();
        }
    }
}