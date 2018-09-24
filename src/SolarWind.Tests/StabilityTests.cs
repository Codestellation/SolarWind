using System;
using System.Threading;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class StabilityTests
    {
        private SolarWindHub _server;
        private Uri _serverUri;
        private SolarWindHub _client;
        private Channel _channelToServer;
        private int _count;
        private int _clientReceived;
        private int _serverReceived;


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

            _count = 10_000;
            _clientReceived = 0;
            _serverReceived = 0;
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
            for (var i = 0; i < _count; i++)
            {
                _channelToServer.Post(new TextMessage {Text = "Hello, server!"});
            }

            Thread.Sleep(10_000);
            new
                {
                    Client = _clientReceived,
                    Server = _serverReceived
                }
                .Should().BeEquivalentTo(
                    new
                    {
                        Client = _count,
                        Server = _count
                    });
        }

        private void OnServerCallback(Channel channel, in MessageHeader messageHeader, object data)
        {
            _serverReceived++;
            channel.Post(new TextMessage {Text = "Hello, client!"});
        }

        private void OnClientCallback(Channel channel, in MessageHeader messageHeader, object data) => _clientReceived++;
    }
}