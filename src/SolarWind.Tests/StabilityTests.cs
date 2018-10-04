using System;
using System.Diagnostics;
using System.Threading;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class ClientStabilityTests
    {
        private SolarWindHub _server;
        private Uri _serverUri;
        private SolarWindHub _client;
        private Channel _channelToServer;
        private int _count;
        private int _clientReceived;
        private int _serverReceived;
        private ManualResetEvent _testCompleted;


        [SetUp]
        public void Setup()
        {
            _serverUri = new Uri("tcp://localhost:4312");

            var jsonNetSerializer = new JsonNetSerializer();

            var solarWindHubOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _server = new SolarWindHub(solarWindHubOptions);

            _server.Listen(new ServerOptions(_serverUri, _ => new ChannelOptions(jsonNetSerializer, OnServerCallback), delegate { }));

            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
            _channelToServer = _client.OpenChannelTo(_serverUri, new ChannelOptions(jsonNetSerializer, OnClientCallback));

            _count = 1_000_000;
            _clientReceived = 0;
            _serverReceived = 0;
            _testCompleted = new ManualResetEvent(false);
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Dispose();
            _client?.Dispose();
        }

        [Test]
        public void PingPong()
        {
            Stopwatch watch = Stopwatch.StartNew();
            for (var i = 0; i < _count; i++)
            {
                _channelToServer.Post(new TextMessage {Text = "Hello, server!"});
            }


            _testCompleted.WaitOne(TimeSpan.FromSeconds(120));
            watch.Stop();

            var expected = new {Client = _clientReceived, Server = _serverReceived};
            var actual = new {Client = _count, Server = _count};

            expected.Should().BeEquivalentTo(actual);

            Console.WriteLine($"Finished in {watch.ElapsedMilliseconds:N3} ms, perf {_count * 1000.0 / watch.ElapsedMilliseconds:N} msg/sec");
        }

        private void OnServerCallback(Channel channel, in MessageHeader messageHeader, object data)
        {
            _serverReceived++;
            channel.Post(new TextMessage {Text = "Hello, client!"});
        }

        private void OnClientCallback(Channel channel, in MessageHeader messageHeader, object data)
        {
            _clientReceived++;
            if (_clientReceived == _count)
            {
                _testCompleted.Set();
            }
        }
    }
}