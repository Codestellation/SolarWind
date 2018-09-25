using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Threading;
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


        [SetUp]
        public void Setup()
        {
            _serverUri = new Uri("tcp://localhost:4312");
            var jsonNetSerializer = new JsonNetSerializer();

            _listener = Build.TcpIPv4();
            StartListener();
            


            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
            _channelToServer = _client.OpenChannelTo(_serverUri, new ChannelOptions(jsonNetSerializer, delegate { }));

            _count = 10_000;

            _serverReceived = 0;
        }

        [TearDown]
        public void TearDown() => _client?.Dispose();

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

            Thread.Sleep(10000);

            _serverReceived.Should().Be(_count);
        }

        private void OnServerCallback(Channel channel, in MessageHeader messageHeader, object data) => _serverReceived++;
    }
}