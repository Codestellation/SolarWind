using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal class Connection
    {
        private readonly AsyncNetworkStream _networkStream;
        private readonly ILogger _logger;
        private readonly DuplexBufferedStream _mainStream;


        private Connection(AsyncNetworkStream networkStream, ILogger logger)
        {
            _networkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _mainStream = new DuplexBufferedStream(_networkStream);
        }

        public async ValueTask Receive(PooledMemoryStream readBuffer, int bytesToReceive, CancellationToken cancellation)
        {
            int left = bytesToReceive;

            while (left != 0)
            {
                if (cancellation.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }

                left -= await readBuffer.WriteAsync(_mainStream, bytesToReceive, cancellation).ConfigureAwait(false);
            }
        }

        public void Close() => _mainStream.Close();

        public async ValueTask WriteAsync(Message message, CancellationToken cancellation)
        {
            _logger.LogDebug($"Writing header {message.Header.ToString()}");
            var wireHeader = new WireHeader(message.Header, new PayloadSize((int)message.Payload.Length));
            byte[] buffer = ArrayPool<byte>.Shared.Rent(WireHeader.Size);

            WireHeader.WriteTo(wireHeader, buffer);
            await _mainStream.WriteAsync(buffer, 0, WireHeader.Size, cancellation).ConfigureAwait(false);
            _logger.LogDebug($"Written header {message.Header.ToString()}");

            ArrayPool<byte>.Shared.Return(buffer);
            _logger.LogDebug($"Writing payload {message.Header.ToString()}");

            await message.Payload.CopyIntoAsync(_mainStream, cancellation);

            _logger.LogDebug($"Written payload {message.Header.ToString()}");
        }

        public static async Task Accept(HubId serverHubId, Socket socket, ILogger logger, Action<HubId, Connection> onAccepted)
        {
            ConfigureSocket(socket);
            var networkStream = new AsyncNetworkStream(socket);
            //SslStream sslStream = null;

            //if (certificate != null)
            //{
            //    sslStream = new SslStream(networkStream, false);
            //    await sslStream
            //        .AuthenticateAsServerAsync(certificate, false, SslProtocols.Tls12, true)
            //        .ConfigureAwait(false);

            //    if (!sslStream.IsAuthenticated)
            //    {
            //        sslStream.Close();
            //        return null;
            //    }
            //}


            HandshakeMessage incoming = await networkStream
                .HandshakeAsServer(serverHubId)
                .ConfigureAwait(false);
            var connection = new Connection(networkStream, logger);
            onAccepted(incoming.HubId, connection);
        }

        public static async void ConnectTo(SolarWindHubOptions options, Uri remoteUri, ILogger logger, Action<Uri, HubId, Connection> onConnected)
        {
            Socket socket = Build.TcpIPv4();
            ConfigureSocket(socket);

            while (true)
            {
                try
                {
                    await socket
                        .ConnectAsync(remoteUri.ResolveRemoteEndpoint())
                        .ConfigureAwait(false);
                    break;
                }
                catch (SocketException e)
                {
                    if (logger.IsEnabled(LogLevel.Trace))
                    {
                        logger.LogTrace(e, $"Cannot connect to '{remoteUri}");
                    }

                    await Task.Delay(5000).ConfigureAwait(false);
                }
            }


            var networkStream = new AsyncNetworkStream(socket);
            //SslStream sslStream = null;
            //if (remoteUri.UseTls())
            //{
            //    sslStream = new SslStream(networkStream, false);
            //    await sslStream
            //        .AuthenticateAsClientAsync(remoteUri.Host)
            //        .ConfigureAwait(false);

            //    if (!sslStream.IsAuthenticated)
            //    {
            //        sslStream.Close();
            //        return null;
            //    }
            //}

            HandshakeMessage handshakeResponse = await networkStream
                .HandshakeAsClient(options.HubId)
                .ConfigureAwait(false);
            var connection = new Connection(networkStream, logger);
            onConnected(remoteUri, handshakeResponse.HubId, connection);
        }

        internal void Receive(PooledMemoryStream readBuffer, int bytesToReceive)
        {
            int left = bytesToReceive;

            while (left != 0)
            {
                left -= readBuffer.Write(_mainStream, bytesToReceive);
            }
        }

        private static void ConfigureSocket(Socket socket)
        {
            socket.ReceiveTimeout = 10_000;
            socket.SendTimeout = 1000;
        }

        public void Write(in Message message)
        {
            //_logger.LogDebug($"Writing header {message.Header.ToString()}");
            var wireHeader = new WireHeader(message.Header, new PayloadSize((int)message.Payload.Length));
            WireHeader.WriteTo(wireHeader, _mainStream);
            //_logger.LogDebug($"Written header {message.Header.ToString()}");

            //_logger.LogDebug($"Writing payload {message.Header.ToString()}");

            message.Payload.CopyInto(_mainStream);

            //_logger.LogDebug($"Written payload {message.Header.ToString()}");
        }

        public void Flush() => _mainStream.Flush();
    }
}