using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class PlainSocketTests
    {
        private SolarWindHub _client;
        private Channel _channelToServer;
        private int _count;


        private IPEndPoint _endpoint;
        private ManualResetEvent _allMessagesReceived;

        private const int msgSize = 64;
        private const int msgCount = 1_000_0;

        [SetUp]
        public void Setup()
        {
            _endpoint = new IPEndPoint(IPAddress.Loopback, 9876);
            _allMessagesReceived = new ManualResetEvent(false);
        }

        private void RunClient()
        {
            Socket client = Build.TcpIPv4();
            client.Connect(_endpoint);
            var stream = new AsyncNetworkStream(client);

            byte[] buffer = Enumerable.Range(1, 64).Select(x => (byte)x).ToArray();
            stream.Write(buffer, 0, msgSize);

            Stopwatch watch = Stopwatch.StartNew();

            for (var i = 0; i < msgCount; i++)
            {
                stream.Write(buffer, 0, msgSize);
            }

            watch.Stop();
            var speed = msgCount * 1000.0 / watch.ElapsedMilliseconds;

            Console.WriteLine($"Send speed {speed:N3} msg/sec");
        }

        private void RunServer()
        {
            Socket listener = Build.TcpIPv4();
            listener.Bind(_endpoint);
            listener.Listen(10);

            var server = new AsyncNetworkStream(listener.Accept());


            var buffer = new byte[1024];
            server.Read(buffer, 0, msgSize);

            Stopwatch watch = Stopwatch.StartNew();
            for (var i = 0; i < msgCount; i++)
            {
                if (server.Read(buffer, 0, msgSize) != msgSize)
                {
                    throw new InvalidOperationException();
                }
            }

            watch.Stop();
            var speed = msgCount * 1000.0 / watch.ElapsedMilliseconds;

            Console.WriteLine($"Receive speed {speed:N3} msg/sec");
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void Should_deliver_a_bunch_of_messages_to_server() => Task.WaitAll(Task.Run(RunServer), Task.Run(RunClient));
    }
}