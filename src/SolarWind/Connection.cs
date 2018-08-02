using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Misc;

namespace Codestellation.SolarWind
{
    public class Connection
    {
        private readonly Socket _socket;
        private readonly NetworkStream _networkStream;
        private readonly SslStream _sslStream;

        /// <summary>
        /// Could be either <see cref="NetworkStream" /> or <see cref="SslStream" />
        /// </summary>
        public Stream Stream { get; }

        public bool IsTls => _sslStream != null;

        private Connection(Socket socket, NetworkStream networkStream, SslStream sslStream = null)
        {
            _socket = socket;
            _networkStream = networkStream;
            _sslStream = sslStream;
            Stream = (Stream)sslStream ?? _networkStream;
        }

        public bool Receive(MemoryStream readBuffer, int bytesToReceive, CancellationToken cancellation)
        {
            if (readBuffer.Capacity < bytesToReceive)
            {
                readBuffer.Capacity = bytesToReceive;
            }

            do
            {
                //TODO: Anylyze how socket error works. 
                if (cancellation.IsCancellationRequested || _socket.Poll(100_000, SelectMode.SelectError))
                {
                    //TODO: Find a better way to 
                    return false;
                }

                if (!_socket.Poll(100_000, SelectMode.SelectRead))
                {
                    continue;
                }

                byte[] buffer = readBuffer.GetBuffer();

                var position = (int)readBuffer.Position;
                int count = bytesToReceive - position;

                readBuffer.Position += Stream.Read(buffer, position, count);
            } while (readBuffer.Position < bytesToReceive);

            readBuffer.Position = 0;
            readBuffer.SetLength(bytesToReceive);
            return true;
        }

        public void Close() => Stream.Close();

        public void Write(byte[] buffer, int offset, int count) => Stream.Write(buffer, offset, count);

        public static async Task<Connection> Accept(Socket socket, X509Certificate certificate = null)
        {
            var networkStream = new NetworkStream(socket, true);
            SslStream sslStream = null;

            if (certificate != null)
            {
                sslStream = new SslStream(networkStream, false);
                await sslStream
                    .AuthenticateAsServerAsync(certificate, false, SslProtocols.Tls12, true)
                    .ConfigureAwait(false);

                if (!sslStream.IsAuthenticated)
                {
                    sslStream.Close();
                    return null;
                }
            }

            return new Connection(socket, networkStream, sslStream);
        }

        public static async Task<Connection> ConnectTo(Uri remoteUri)
        {
            Socket socket = Build.TcpIPv4();
            await socket
                .ConnectAsync(remoteUri.ResolveRemoteEndpoint())
                .ConfigureAwait(false);

            var networkStream = new NetworkStream(socket, true);
            SslStream sslStream = null;
            if (remoteUri.UseTls())
            {
                sslStream = new SslStream(networkStream, false);
                await sslStream
                    .AuthenticateAsClientAsync(remoteUri.Host)
                    .ConfigureAwait(false);

                if (!sslStream.IsAuthenticated)
                {
                    sslStream.Close();
                    return null;
                }
            }

            return new Connection(socket, networkStream, sslStream);
        }
    }
}