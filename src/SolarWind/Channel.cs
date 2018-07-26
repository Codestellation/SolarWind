using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace Codestellation.SolarWind
{
    public delegate void SolarWindCallback(Channel channel, Message message);

    public class Channel
    {
        private readonly Socket _socket;
        private readonly Stream _tcpStream;
        private readonly SolarWindHubOptions _options;
        private bool _disposed;

        private Task _reader;
        private Task _writer;
        private readonly BlockingCollection<Message> _outgoings;
        private readonly MemoryStream _writeBuffer;
        private readonly MemoryStream _readBuffer;


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
            _outgoings = new BlockingCollection<Message>();

            _writeBuffer = new MemoryStream(1024);
            _readBuffer = new MemoryStream(1024);

            _reader = StartReadingTask();
            _writer = StartWritingTask();
        }

        public void Post(Message message) => _outgoings.Add(message);

        private async Task StartReadingTask()
        {
            while (!_disposed)
            {
                await Receive(Header.Size).ConfigureAwait(false);
                Header header = Header.ReadFrom(_readBuffer.GetBuffer());

                await Receive(header.PayloadSize.Value).ConfigureAwait(false);

                object payload = _options.Serializer.Deserialize(header.MessageId, _tcpStream);
                var message = new Message(header.MessageId, payload);
                _options.Callback(this, message);
            }
        }

        private Task Receive(int bytesToReceive) => Task.Run(() =>
        {
            if (_readBuffer.Capacity < bytesToReceive)
            {
                _readBuffer.Capacity = bytesToReceive;
            }

            while (_readBuffer.Position < bytesToReceive)
            {
                byte[] buffer = _readBuffer.GetBuffer();

                var position = (int)_readBuffer.Position;
                var count = bytesToReceive - position;
                _readBuffer.Position += _socket.Receive(buffer, position, count, SocketFlags.None);
                //_readBuffer.Position += _tcpStream.Read(buffer, position, count);
            }

            _readBuffer.Position = 0;
            _readBuffer.SetLength(bytesToReceive);
        });

        private Task StartWritingTask() => Task.Run(() =>
        {
            foreach (Message message in _outgoings.GetConsumingEnumerable())
            {
                _writeBuffer.SetLength(Header.Size);
                _writeBuffer.Position = Header.Size;

                _options.Serializer.Serialize(message.Payload, _writeBuffer);

                var header = new Header(message.MessageTypeId, PayloadSize.From(_writeBuffer, Header.Size));
                byte[] buffer = _writeBuffer.GetBuffer();
                Header.WriteTo(in header, buffer);
                _tcpStream.Write(buffer, 0, (int)_writeBuffer.Position);
            }
        });
    }
}