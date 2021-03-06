using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class SendStabilityTests
    {
        private Uri _serverUri;
        private SolarWindHub _client;
        private Channel _channelToServer;
        private int _count;

        private int _serverReceived;
        private Socket _listener;
        private IPEndPoint _endpoint;
        private ManualResetEvent _allMessagesReceived;


        [SetUp]
        public void Setup()
        {
            _serverUri = new Uri("tcp://localhost:4312");
            _endpoint = new IPEndPoint(IPAddress.Loopback, _serverUri.Port);
            var jsonNetSerializer = new JsonNetSerializer();

            _listener = Build.TcpIPv4();
            _listener.Bind(_endpoint);
            _listener.Listen(10);
            Task.Run(StartListener);

            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
            _channelToServer = _client.OpenChannelTo(_serverUri, new ChannelOptions(jsonNetSerializer, delegate { }));

            _count = 1_000_000;

            _serverReceived = 0;

            _allMessagesReceived = new ManualResetEvent(false);
        }

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

                    _serverReceived++;

                    if (_serverReceived == _count + 1)
                    {
                        _allMessagesReceived.Set();
                    }
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

        [TearDown]
        public void TearDown()
        {
            _listener.Dispose();
            _client?.Dispose();
        }

        [Test]
        public void Should_deliver_a_bunch_of_messages_to_server()
        {
            _channelToServer.Post(new TextMessage {Text = $"Hello {(-1).ToString(CultureInfo.InvariantCulture)}, server !"});
            Stopwatch watch = Stopwatch.StartNew();
            for (var i = 0; i < _count; i++)
            {
                _channelToServer.Post(new TextMessage {Text = $"Hello {i.ToString(CultureInfo.InvariantCulture)}, server !"});
            }

            Console.WriteLine($"Pushed all messages in {watch.ElapsedMilliseconds}");
            GCStats before = GCStats.Snapshot();
            for (var times = 0; times < 60; times++)
            {
                if (_allMessagesReceived.WaitOne(TimeSpan.FromSeconds(1)))
                {
                    break;
                }

                Console.WriteLine($"{times:D3}: received {_serverReceived.ToString(CultureInfo.InvariantCulture)} messages.");
            }

            watch.Stop();
            GCStats diff = before.Diff();
            Console.WriteLine($"Finished in {watch.ElapsedMilliseconds:N3} ms, perf {_count * 1000.0 / watch.ElapsedMilliseconds:N} msg/sec");
            Console.WriteLine(diff);

            _serverReceived.Should().Be(_count + 1);
        }
    }
}