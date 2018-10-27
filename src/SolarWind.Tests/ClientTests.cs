using System;
using System.Threading;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class ClientTests
    {
        private SolarWindHub _client;
        private AutoResetEvent _received;

        [SetUp]
        public void Setup()
        {
            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
            _received = new AutoResetEvent(false);
        }

        [TearDown]
        public void Teardown() => _client?.Dispose();



        [Test]
        public void Should_connect_if_server_starts_later()
        {
            const int port = 4564;
            var options = new ChannelOptions(JsonNetSerializer.Instance, delegate { });
            var channel = _client.OpenChannelTo(new Uri($"tcp://localhost:{port}"), options);

            SendMessage(port, channel);
            Thread.Sleep(10*1000);
            SendMessage(port, channel);
        }

        private void SendMessage(int port, Channel channel)
        {
            using (new TestServer(OnReceived, port))
            {
                channel.Post(TextMessage.New());

                _received.WaitOne(TimeSpan.FromSeconds(1)).Should().BeTrue("Did not receive the first message");
            }
        }

        private void OnReceived(MessageHeader header, PooledMemoryStream payload)
        {
            Console.WriteLine(header);
            _received.Set();
        }
    }
}