using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Codestellation.SolarWind.Threading;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal class Connection : IDisposable
    {
        private readonly AsyncNetworkStream _networkStream;
        private readonly ILogger _logger;
        private readonly Action _reconnect;
        private readonly DuplexBufferedStream _mainStream;
        private bool _disposed;
        public bool Connected => !_disposed;

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
            try
            {
                var left = bytesToReceive;
                while (left != 0)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }

                    left -= await readBuffer.WriteAsync(_mainStream, bytesToReceive, cancellation).ConfigureAwait(ContinueOn.IOScheduler);
                }
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException("Receive failed", e);
            }
        }

        public ValueTask WriteAsync(in Message message, CancellationToken cancellation)
        {
            try
            {
                _logger.LogDebug($"Writing message {message.Header.ToString()}");
                var wireHeader = new WireHeader(message.Header, new PayloadSize((int)message.Payload.Length));
                WireHeader.WriteTo(wireHeader, _mainStream);
                return message.Payload.CopyIntoAsync(_mainStream, cancellation);
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException("Receive failed", e);
            }
        }

        public static async Task Accept(SolarWindHubOptions options, Socket socket, Action<HubId, Connection> onAccepted)
        {
            ILogger<Connection> logger = options.LoggerFactory.CreateLogger<Connection>();

            AsyncNetworkStream networkStream = null;
            try
            {
                ConfigureSocket(socket, options);
                networkStream = new AsyncNetworkStream(socket);

                logger.LogInformation("Begin handshake as server");

                HandshakeMessage incoming = await networkStream
                    .HandshakeAsServer(options.HubId, logger)
                    .ConfigureAwait(false);

                logger.LogInformation("End handshake as server");

                var connection = new Connection(networkStream, options.LoggerFactory.CreateLogger<Connection>(), null);
                onAccepted(incoming.HubId, connection);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exception during handshake");
                networkStream?.Dispose();
            }
        }

        public static async void ConnectTo(SolarWindHubOptions options, Uri remoteUri, Action<Uri, HubId, Connection> onConnected)
        {
            (HandshakeMessage handshake, AsyncNetworkStream stream) = await DoConnect(options, remoteUri).ConfigureAwait(false);

            Action reconnect = () => ConnectTo(options, remoteUri, onConnected);
            ILogger<Connection> logger = options.LoggerFactory.CreateLogger<Connection>();


            var connection = new Connection(stream, logger, reconnect);
            onConnected(remoteUri, handshake.HubId, connection);
        }

        private static async Task<(HandshakeMessage handshake, AsyncNetworkStream stream)> DoConnect(SolarWindHubOptions options, Uri remoteUri)
        {
            ILogger<Connection> logger = options.LoggerFactory.CreateLogger<Connection>();

            while (true)
            {
                IPEndPoint[] remoteEp = remoteUri.ResolveRemoteEndpoint();
                foreach (IPEndPoint ipEndPoint in remoteEp)
                {
                    (HandshakeMessage handshake, AsyncNetworkStream stream) =
                        await TryConnectoTo(remoteUri, ipEndPoint, logger, options)
                            .ConfigureAwait(false);

                    if (handshake != null)
                    {
                        return (handshake, stream);
                    }
                }

                await Task.Delay(5000).ConfigureAwait(false);
            }
        }

        private static async Task<(HandshakeMessage handshakeResponse, AsyncNetworkStream networkStream)> TryConnectoTo(
            Uri remoteUri,
            IPEndPoint remoteEp,
            ILogger logger,
            SolarWindHubOptions options)
        {
            Socket socket = remoteEp.BuildTcpSocket();
            try
            {
                logger.LogInformation($"Connecting to '{remoteUri}' ({remoteEp})");

                await socket
                    .ConnectAsync(remoteEp)
                    .ConfigureAwait(false);

                logger.LogInformation($"Connected to '{remoteUri}' ({remoteEp})");

                var networkStream = new AsyncNetworkStream(socket);

                HandshakeMessage handshakeResponse = await networkStream
                    .HandshakeAsClient(options.HubId, logger)
                    .ConfigureAwait(false);

                logger.LogInformation($"Successfully connected to '{handshakeResponse.HubId}' ({remoteUri})");

                return (handshakeResponse, networkStream);
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation($"Cannot connect to '{remoteUri}' ({remoteEp}). Reason: {ex.Message}");
                }

                socket.SafeDispose();
            }

            return (null, null);
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
            _disposed = true;
            _mainStream.Close();
            _mainStream.Dispose();
        }
    }
}