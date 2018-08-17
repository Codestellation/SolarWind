using System;
using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Internals
{
    public class Connection
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

        public static async Task<Connection> Accept(Socket socket, X509Certificate certificate = null)
        {
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

            return new Connection(networkStream);
        }

        public static async void ConnectTo(SolarWindHubOptions options, Uri remoteUri, Action<Uri, HubId, Connection> onConnected)
        {
            Socket socket = Build.TcpIPv4();
            await socket
                .ConnectAsync(remoteUri.ResolveRemoteEndpoint())
                .ConfigureAwait(false);

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
    }
}