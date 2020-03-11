using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Codestellation.SolarWind;
using Codestellation.SolarWind.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Benchmark
{
    [SimpleJob(RuntimeMoniker.Net472)]
    [SimpleJob(RuntimeMoniker.NetCoreApp21)]
    [SimpleJob(RuntimeMoniker.NetCoreApp31)]
    [HtmlExporter]
    [MemoryDiagnoser]
    public class PingPongBenchmark
    {
        private Uri _serverUri;
        private SolarWindHub _server;
        private SolarWindHub _client;
        private Channel _channelToServer;

        private int _count;
        private int _clientReceived;
        private int _serverReceived;
        private AutoResetEvent _testCompleted;

        [Params(64, 128, 256, 512, 1024, 2048)]
        public int MessageSize { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            _serverUri = new Uri("tcp://localhost:4312");

            var solarWindHubOptions = new SolarWindHubOptions(NullLoggerFactory.Instance);
            _server = new SolarWindHub(solarWindHubOptions);

            _server.Listen(new ServerOptions(_serverUri, _ => new ChannelOptions(NullSerializer.Instance, OnServerCallback), delegate { }));

            var clientOptions = new SolarWindHubOptions(NullLoggerFactory.Instance);
            _client = new SolarWindHub(clientOptions);
            _channelToServer = _client.OpenChannelTo(_serverUri, new ChannelOptions(NullSerializer.Instance, OnClientCallback));

            _count = 300_000;
            _clientReceived = 0;
            _serverReceived = 0;
            _testCompleted = new AutoResetEvent(false);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _client.Dispose();
            _server.Dispose();
            _clientReceived = 0;
            _serverReceived = 0;
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _clientReceived = 0;
            _serverReceived = 0;
            NullSerializer.Instance.MessageSize = MessageSize;
        }

        [Benchmark]
        public void Run_ping_pong_benchmark()
        {
            for (var i = 0; i < _count; i++)
            {
                _channelToServer.Post(NullSerializer.Dummy);
            }

            if (!_testCompleted.WaitOne(TimeSpan.FromSeconds(60)))
            {
                var message = $"Messages were not received (Client={_clientReceived} Server={_serverReceived})";
                throw new ApplicationException(message);
            }
        }

        private void OnServerCallback(Channel channel, in MessageHeader messageHeader, object data)
        {
            _serverReceived++;
            channel.Post(NullSerializer.Instance);
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