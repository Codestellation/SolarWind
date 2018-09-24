using System;
using System.Diagnostics;
using System.Threading;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
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

            var solarWindHubOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _server = new SolarWindHub(solarWindHubOptions);

            _server.Listen(new ServerOptions(_serverUri, _ => new ChannelOptions(jsonNetSerializer, OnServerCallback), delegate { }));

            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
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
            _channelToServer.Post(new TextMessage {Text = "Hello, server!"});
            TimeSpan timeout = Debugger.IsAttached ? TimeSpan.FromMinutes(10) : TimeSpan.FromSeconds(1);
            _serverReceivedMessage.WaitOne(timeout).Should().BeTrue("server is expected to receive the request");
            _clientReceivedMessage.WaitOne(timeout).Should().BeTrue("client is expected to receive the response");
        }

        private void OnServerCallback(Channel channel, in MessageHeader messageHeader, object data)
        {
            Console.WriteLine("Message from client:");
            Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
            channel.Post(new TextMessage {Text = "Hello, client!"});
            _serverReceivedMessage.Set();
        }

        private void OnClientCallback(Channel channel, in MessageHeader messageHeader, object data)
        {
            Console.WriteLine("Message from server:");
            Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
            _clientReceivedMessage.Set();
        }
    }
}