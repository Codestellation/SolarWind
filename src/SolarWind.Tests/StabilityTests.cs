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
    public class StabilityTests
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

            _count = 1_000_0;

            _serverReceived = 0;

            _allMessagesReceived = new ManualResetEvent(false);
        }

        private void StartListener()
        {
            var server = new AsyncNetworkStream(_listener.Accept());
            HandshakeMessage handshake = server.ReceiveHandshake().Result;

            server.SendHandshake(HubId.Generate());

            var buffer = new byte[1024];
            while (true)
            {
                var read = server.Read(buffer, 0, WireHeader.Size); // receive wire header
                WireHeader header = WireHeader.ReadFrom(buffer);

                if (read != WireHeader.Size)
                {
                    throw new InvalidOperationException();
                }

                if (server.Read(buffer, 0, header.PayloadSize.Value) != header.PayloadSize.Value)
                {
                    throw new InvalidOperationException();
                }

                _serverReceived++;

                if (_serverReceived == _count + 1)
                {
                    _allMessagesReceived.Set();
                }
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

            watch.Stop();

            Console.WriteLine($"Pushed all messages in {watch.ElapsedMilliseconds}");

            var previous = 0;
            var times = 200;
            while (!_allMessagesReceived.WaitOne(TimeSpan.FromSeconds(1)))
            {
                if (--times == 0)
                {
                    break;
                }

                if (_serverReceived > previous)
                {
                    previous = _serverReceived;
                    continue;
                }

                break;
            }


            _serverReceived.Should().Be(_count + 1);
        }
    }
}