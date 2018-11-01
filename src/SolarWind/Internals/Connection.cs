using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal class Connection : IDisposable
    {
        private readonly AsyncNetworkStream _networkStream;
        private readonly ILogger _logger;
        private readonly Action _reconnect;
        private readonly DuplexBufferedStream _mainStream;


        private Connection(AsyncNetworkStream networkStream, ILogger logger, Action reconnect)
        {
            _networkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reconnect = reconnect;

            _mainStream = new DuplexBufferedStream(_networkStream);
        }

        public void Reconnect() => _reconnect?.Invoke();


        public async ValueTask ReceiveAsync(PooledMemoryStream readBuffer, int bytesToReceive, CancellationToken cancellation)
        {
            var left = bytesToReceive;

            while (left != 0)
            {
                if (cancellation.IsCancellationRequested)
                {
                    throw new TaskCanceledException();
                }

                left -= await readBuffer.WriteAsync(_mainStream, bytesToReceive, cancellation).ConfigureAwait(false);
            }
        }

        public ValueTask WriteAsync(in Message message, CancellationToken cancellation)
        {
            _logger.LogDebug($"Writing message {message.Header.ToString()}");
            var wireHeader = new WireHeader(message.Header, new PayloadSize((int)message.Payload.Length));
            WireHeader.WriteTo(wireHeader, _mainStream);
            return message.Payload.CopyIntoAsync(_mainStream, cancellation);
        }

        public static async Task Accept(SolarWindHubOptions options, Socket socket, Action<HubId, Connection> onAccepted)
        {
            ConfigureSocket(socket, options);
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
                .HandshakeAsServer(options.HubId)
                .ConfigureAwait(false);
            var connection = new Connection(networkStream, options.LoggerFactory.CreateLogger<Connection>(), null);
            onAccepted(incoming.HubId, connection);
        }

        public static async void ConnectTo(SolarWindHubOptions options, Uri remoteUri, ILogger logger, Action<Uri, HubId, Connection> onConnected)
        {
            Socket socket = Build.TcpIPv4();
            ConfigureSocket(socket, options);

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


            Action reconnect = () => ConnectTo(options, remoteUri, logger, onConnected);

            var connection = new Connection(networkStream, logger, reconnect);
            onConnected(remoteUri, handshakeResponse.HubId, connection);
        }


        private static void ConfigureSocket(Socket socket, SolarWindHubOptions options)
        {
            socket.NoDelay = options.NoDelay;
            socket.ReceiveTimeout = (int)options.ReceiveTimeout.TotalMilliseconds;
            socket.SendTimeout = (int)options.SendTimeout.TotalMilliseconds;
            socket.LingerState = new LingerOption(true, 1);
        }

        public Task FlushAsync(CancellationToken cancellation) => _mainStream.FlushAsync(cancellation);

        public void Dispose()
        {
            _mainStream.Close();
            _mainStream.Dispose();
        }
    }
}