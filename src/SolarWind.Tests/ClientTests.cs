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
        private CountdownEvent _received;

        [SetUp]
        public void Setup()
        {
            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
            _received = new CountdownEvent(1);
        }

        [TearDown]
        public void Teardown() => _client?.Dispose();


        [Test]
        public void Should_connect_if_server_starts_later()
        {
            const int port = 4564;
            var options = new ChannelOptions(JsonNetSerializer.Instance, delegate { });
            Channel channel = _client.OpenChannelTo(new Uri($"tcp://localhost:{port}"), options);

            SendMessage(port, channel, "Did not receive the first message");
            Thread.Sleep(5 * 1000);
            SendMessage(port, channel, "Did not receive the second message");
        }

        private void SendMessage(int port, Channel channel, string message)
        {
            using (new TestServer(OnReceived, port))
            {
                channel.Post(TextMessage.New());

                _received.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)).Should().BeTrue(message);

                _received.Reset();
            }
        }

        private void OnReceived(MessageHeader header, PooledMemoryStream payload)
        {
            Console.WriteLine(header);
            _received.Signal();
        }
    }
}