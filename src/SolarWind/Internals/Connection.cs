using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
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
        private readonly CancellationToken _cancellation;
        private bool _disposed;
        private readonly byte[] _readBuffer;
        private int _readPosition;
        private int _readLength;

        private readonly byte[] _writeBuffer;
        private int _writePosition;

        public bool Connected => !_disposed;

        private Connection(AsyncNetworkStream networkStream, ILogger logger, Action reconnect, CancellationToken cancellation)
        {
            _networkStream = networkStream ?? throw new ArgumentNullException(nameof(networkStream));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reconnect = reconnect;
            _cancellation = cancellation;
            _readBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
            _writeBuffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        }

        public void Reconnect()
        {
            if (_cancellation.IsCancellationRequested)
            {
                return;
            }

            _reconnect?.Invoke();
        }


        public async ValueTask ReceiveAsync(PooledMemoryStream destination, int bytesToReceive, CancellationToken cancellation)
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

                    var available = _readLength - _readPosition;
                    if (available == 0)
                    {
                        _readPosition = 0;
                        _readLength = 0;
                        var memory = new Memory<byte>(_readBuffer, 0, _readBuffer.Length);
                        _readLength = await _networkStream
                            .ReadAsync(memory, cancellation)
                            .ConfigureAwait(false);
                        available = _readLength;
                    }

                    var bytesToStream = Math.Min(available, left);
                    destination.Write(_readBuffer, _readPosition, bytesToStream);
                    _readPosition += bytesToStream;
                    left -= bytesToStream;
                }
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException("Receive failed", e);
            }
        }

        public async ValueTask WriteAsync(Message message, CancellationToken cancellation)
        {
            try
            {
                PooledMemoryStream payload = message.Payload;
                long payloadLength = payload.Length;

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug($"Writing message {message.Header.ToString()} size={payloadLength}");
                }

                var available = _writeBuffer.Length - _writePosition;
                var payloadSize = new PayloadSize((int)payloadLength);
                var wireHeader = new WireHeader(message.Header, payloadSize);

                if (available < WireHeader.Size)
                {
                    await FlushAsync(cancellation).ConfigureAwait(false);
                }

                WireHeader.WriteTo(in wireHeader, _writeBuffer, _writePosition);
                _writePosition += WireHeader.Size;

                var bytesToSend = (int)payloadLength;
                payload.Position = 0;
                while (bytesToSend > 0)
                {
                    available = _writeBuffer.Length - _writePosition;

                    if (available == 0)
                    {
                        await FlushAsync(cancellation).ConfigureAwait(false);
                    }

                    var sliceSize = Math.Min(available, bytesToSend);
                    var readFromPayload = payload.Read(_writeBuffer, _writePosition, sliceSize);

                    Debug.Assert(sliceSize == readFromPayload);

                    bytesToSend -= readFromPayload;
                    _writePosition += readFromPayload;
                }
            }
            catch (ObjectDisposedException e)
            {
                throw new IOException("Send failed", e);
            }
        }

        public static async Task Accept(SolarWindHubOptions options, Socket socket, Action<HubId, Connection> onAccepted)
        {
            ILogger<Connection> logger = options.ConnectionLogger;

            AsyncNetworkStream networkStream = null;
            try
            {
                ConfigureSocket(socket, options);
                networkStream = new AsyncNetworkStream(socket);

                logger.LogInformation("Begin handshake as server");

                HandshakeMessage incoming = await networkStream
                    .HandshakeAsServer(options.HubId, logger, options.Cancellation)
                    .ConfigureAwait(false);

                logger.LogInformation("End handshake as server");

                var connection = new Connection(networkStream, logger, null, options.Cancellation);
                onAccepted(incoming.HubId, connection);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Exception during handshake");
                networkStream?.Dispose();
            }
        }

        public static async void ConnectTo(
            SolarWindHubOptions options,
            Uri remoteUri,
            Action<Uri, HubId, Connection> onConnected)
        {
            (HandshakeMessage handshake, AsyncNetworkStream stream) = await DoConnect(options, remoteUri).ConfigureAwait(false);

            if (options.Cancellation.IsCancellationRequested)
            {
                return;
            }

            Action reconnect = () => ConnectTo(options, remoteUri, onConnected);
            ILogger<Connection> logger = options.ConnectionLogger;

            var connection = new Connection(stream, logger, reconnect, options.Cancellation);
            onConnected(remoteUri, handshake.HubId, connection);
        }

        private static async Task<(HandshakeMessage handshake, AsyncNetworkStream stream)> DoConnect(SolarWindHubOptions options, Uri remoteUri)
        {
            if (!options.Cancellation.IsCancellationRequested)
            {
                try
                {
                    ILogger<Connection> logger = options.ConnectionLogger;
                    while (!options.Cancellation.IsCancellationRequested)
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

                        await Task.Delay(5000, options.Cancellation).ConfigureAwait(false);
                    }
                }
                catch (TaskCanceledException)
                {
                }
            }

            return (null, null);
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
                    .HandshakeAsClient(options.HubId, logger, options.Cancellation)
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

        public ValueTask FlushAsync(CancellationToken cancellation)
        {
            var length = _writePosition;
            _writePosition = 0; //Zero it here to avoid making the method async
            var memory = new ReadOnlyMemory<byte>(_writeBuffer, 0, length);
            return _networkStream.WriteAsync(memory, cancellation);
        }

        public void Dispose()
        {
            _disposed = true;
            ArrayPool<byte>.Shared.Return(_readBuffer);
            ArrayPool<byte>.Shared.Return(_writeBuffer);
            _networkStream.Close();
            _networkStream.Dispose();
        }
    }
}