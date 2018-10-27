using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Threading;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using JetBrains.dotMemoryUnit;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class SendStabilityTests
    {
        [SetUp]
        public void Setup()
        {
            _server = new TestServer(OnServerReceived);

            var clientOptions = new SolarWindHubOptions(TestContext.LoggerFactory);
            _client = new SolarWindHub(clientOptions);
            _channelToServer = _client.OpenChannelTo(_server.ListenAt, new ChannelOptions(JsonNetSerializer.Instance, delegate { }));

            _count = 1_000_000;

            _serverReceived = 0;

            _allMessagesReceived = new ManualResetEvent(false);
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Dispose();
            _client?.Dispose();
        }

        private SolarWindHub _client;
        private Channel _channelToServer;
        private int _count;

        private int _serverReceived;

        private ManualResetEvent _allMessagesReceived;
        private TestServer _server;

        private void OnServerReceived(MessageHeader header, PooledMemoryStream payload)
        {
            _serverReceived++;

            if (_serverReceived == _count + 1)
            {
                _allMessagesReceived.Set();
            }
        }

        [DotMemoryUnit(CollectAllocations=true)]
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