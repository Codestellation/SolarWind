using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Internals
{
    internal class Connection
    {
        private readonly AsyncNetworkStream _networkStream;

        /// <summary>
        /// Could be either <see cref="NetworkStream" /> or <see cref="SslStream" />
        /// </summary>
        public AsyncNetworkStream Stream { get; }

        private Connection(AsyncNetworkStream networkStream)
        {
            _networkStream = networkStream;
            Stream = _networkStream;
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

                left -= await readBuffer.WriteFromAsync(Stream, bytesToReceive, cancellation).ConfigureAwait(false);
            }

            readBuffer.CompleteWrite();
        }

        public void Close() => Stream.Close();

        public async ValueTask WriteAsync(Message message, CancellationToken cancellation)
        {
            var wireHeader = new WireHeader(message.Header, new PayloadSize((int)message.Payload.Length));
            byte[] buffer = ArrayPool<byte>.Shared.Rent(WireHeader.Size);

            WireHeader.WriteTo(wireHeader, buffer);
            await Stream.WriteAsync(buffer, 0, WireHeader.Size, cancellation).ConfigureAwait(false);

            ArrayPool<byte>.Shared.Return(buffer);

            await message.Payload.CopyIntoAsync(Stream, cancellation);
        }

        public static async Task Accept(HubId serverHubId, Socket socket, Action<HubId, Connection> onAccepted)
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
            var connection = new Connection(networkStream);
            onAccepted(incoming.HubId, connection);
        }

        public static async void ConnectTo(SolarWindHubOptions options, Uri remoteUri, Action<Uri, HubId, Connection> onConnected)
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
                    Console.WriteLine(e.Message);
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
            var connection = new Connection(networkStream);
            onConnected(remoteUri, handshakeResponse.HubId, connection);
        }

        private static void ConfigureSocket(Socket socket) => socket.ReceiveTimeout = 10_000;
    }
}