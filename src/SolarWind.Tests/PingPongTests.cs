using System;
using System.Diagnostics;
using System.Threading;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class PingPongTests
    {
        private SolarWindHub _server;
        private Uri _serverUri;
        private SolarWindHub _client;
        private Channel _channelToServer;
        private ManualResetEvent _serverReceivedMessage;
        private ManualResetEvent _clientReceivedMessage;

        [SetUp]
        public void Setup()
        {
            _serverUri = new Uri("tcp://localhost:4312");

            var jsonNetSerializer = new JsonNetSerializer();

            _server = new SolarWindHub(new SolarWindHubOptions(_ => new ChannelOptions(jsonNetSerializer, OnServerCallback)));

            _server.Listen(_serverUri);

            _client = new SolarWindHub(new SolarWindHubOptions(_ => new ChannelOptions(jsonNetSerializer, delegate { })));
            _channelToServer = _client.OpenChannelTo(_serverUri, new ChannelOptions(jsonNetSerializer, OnClientCallback));

            _serverReceivedMessage = new ManualResetEvent(false);
            _clientReceivedMessage = new ManualResetEvent(false);
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
            _channelToServer.Post(new MessageTypeId(1), new TextMessage {Text = "Hello, server!"});
            TimeSpan timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(1);
            _serverReceivedMessage.WaitOne(timeout).Should().BeTrue("server is expected to receive the request");
            _clientReceivedMessage.WaitOne(timeout).Should().BeTrue("client is expected to receive the response");
        }

        private void OnServerCallback(Channel channel, MessageHeader header, object data)
        {
            Console.WriteLine("Message from client:");
            Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
            channel.Post(new MessageTypeId(1), new TextMessage {Text = "Hello, client!"});
            _serverReceivedMessage.Set();
        }

        private void OnClientCallback(Channel channel, MessageHeader header, object data)
        {
            Console.WriteLine("Message from server:");
            Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
            _clientReceivedMessage.Set();
        }
    }
}