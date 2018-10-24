using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Tests
{
    public class TestServer
    {
        private readonly Action<MessageHeader, PooledMemoryStream> _callback;
        private readonly Socket _listener;

        public TestServer(Action<MessageHeader, PooledMemoryStream> callback)
        {
            _callback = callback;
            _listener = Build.TcpIPv4();
            _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            _listener.Listen(10);
            Task.Run(StartListener);
        }

        public Uri ListenAt => new Uri($"tcp://localhost:{((IPEndPoint)_listener.LocalEndPoint).Port}");

        private void StartListener()
        {
            var server = new AsyncNetworkStream(_listener.Accept());
            HandshakeMessage handshake = server.ReceiveHandshake().Result;

            server.SendHandshake(HubId.Generate());

            using (var buffer = new PooledMemoryStream())
            {
                while (true)
                {
                    buffer.Reset();

                    Receive(server, buffer, WireHeader.Size);
                    buffer.Position = 0;
                    WireHeader header = WireHeader.ReadFrom(buffer);

                    //Console.WriteLine($"Received {header.MessageHeader} ({header.PayloadSize.Value.ToString(CultureInfo.InvariantCulture)})");
                    Receive(server, buffer, header.PayloadSize.Value);
                    buffer.Position = 0;
                    _callback(header.MessageHeader, buffer);
                }
            }
        }

        internal void Receive(NetworkStream from, PooledMemoryStream to, int bytesToReceive)
        {
            var left = bytesToReceive;
            while (left != 0)
            {
                left -= to.Write(from, bytesToReceive);
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}