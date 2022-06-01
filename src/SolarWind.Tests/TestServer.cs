using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Tests
{
    public class TestServer : IDisposable
    {
        private readonly Action<MessageHeader, PooledMemoryStream> _callback;
        private readonly Socket _listener;
        private Socket _client;

        public Uri ListenAt => new Uri($"tcp://localhost:{((IPEndPoint)_listener.LocalEndPoint).Port}");

        public TestServer(Action<MessageHeader, PooledMemoryStream> callback, int port = 0)
        {
            _callback = callback;
            _listener = Build.TcpIPv4();
            _listener.ReceiveTimeout = Timeout.Infinite;
            _listener.Bind(new IPEndPoint(IPAddress.Loopback, port));
            _listener.Listen(10);
            Task.Run(StartListener);
        }

        private void StartListener()
        {
            _client = _listener.Accept();
            _client.ReceiveTimeout = Timeout.Infinite;
            var server = new AsyncNetworkStream(_client);
            HandshakeMessage handshake = server.ReceiveHandshake(CancellationToken.None).Result;
            Console.WriteLine(handshake.HubId);
            server.SendHandshake(HubId.Generate(), CancellationToken.None);

            using (var buffer = new PooledMemoryStream())
            {
                while (_client.Connected)
                {
                    try
                    {
                        Receive(buffer, server);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void Receive(PooledMemoryStream buffer, AsyncNetworkStream server)
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

        private void Receive(NetworkStream from, PooledMemoryStream to, int bytesToReceive)
        {
            var left = bytesToReceive;
            while (left != 0)
            {
                left -= to.Write(from, bytesToReceive);
            }
        }

        public void Dispose()
        {
            _listener.Dispose();
            _client.Dispose();
        }
    }
}