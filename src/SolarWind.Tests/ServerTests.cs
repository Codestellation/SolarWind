using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class ServerTests
    {
        public class AsyncTcpClient : TcpClient
        {
            private AsyncNetworkStream _stream;
            public AsyncNetworkStream Stream => _stream ?? (_stream = new AsyncNetworkStream(Client));
        }

        private SolarWindHub _hub;
        private Uri _uri;
        private MemoryStream _messageBuffer;
        private HubId _clientHubId;
        private ManualResetEvent _keepAliveRaised;

        [SetUp]
        public void Setup()
        {
            var options = new SolarWindHubOptions(TestContext.LoggerFactory);

            _hub = new SolarWindHub(options);
            _uri = new Uri("tcp://localhost:4312");
            _hub.Listen(new ServerOptions(_uri, _ => new ChannelOptions(JsonNetSerializer.Instance, OnCallback) {KeepAliveTimeout = TimeSpan.FromSeconds(3)}, RaiseTimeOutEvent));


            var header = new MessageHeader(new MessageTypeId(1), MessageId.Empty, MessageId.Empty);
            var data = new TextMessage {Text = "Greetings"};

            _messageBuffer = new MemoryStream();

            JsonNetSerializer.Instance.SerializeMessage(_messageBuffer, in header, data);

            _clientHubId = new HubId("client");
            _keepAliveRaised = new ManualResetEvent(false);
        }

        private void RaiseTimeOutEvent(ChannelId channelid, Channel channel)
            => channel.OnKeepAliveTimeout += _ => _keepAliveRaised.Set();

        [TearDown]
        public void TearDown() => _hub.Dispose();

        [Test]
        public void Should_respond_to_single_packet_message()
        {
            using (TcpClient client = CreateClient())
            {
                client.Client.Send(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, SocketFlags.None);

                AssertReceived(client);
            }
        }

        [Test]
        public void Should_respond_to_multi_packet_message()
        {
            using (TcpClient client = CreateClient())
            {
                const int chunkSize = 7;
                for (var i = 0; i < 12; i++)
                {
                    client.Client.Send(_messageBuffer.GetBuffer(), i * chunkSize, chunkSize, SocketFlags.None);
                    Thread.Sleep(100);
                }

                AssertReceived(client);
            }
        }


        [Test]
        public void Should_handle_disconnect_gracefully()
        {
            using (TcpClient client = CreateClient())
            {
                const int chunkSize = 7;
                for (var i = 0; i < 6; i++)
                {
                    client.Client.Send(_messageBuffer.GetBuffer(), i * chunkSize, chunkSize, SocketFlags.None);
                    Thread.Sleep(100);
                }

                client.Close();
            }

            Thread.Sleep(1000);

            using (TcpClient client = CreateClient())
            {
                client.Client.Send(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, SocketFlags.None);

                AssertReceived(client);
            }
        }


        [Test]
        public void Should_raise_on_keep_alive_timeout_event()
        {
            using (TcpClient client = CreateClient())
            {
                client.Client.Send(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, SocketFlags.None);

                AssertReceived(client);
                client.Close();
            }

            Assert.That(_keepAliveRaised.WaitOne(TimeSpan.FromSeconds(10)));
        }

        [Test]
        public void Should_drop_invalid_connections()
        {
            using (var client = new TcpClient())
            {
                client.Connect(_uri.Host, _uri.Port);

                client.ReceiveTimeout = 10_000;
                client.GetStream().Read(new byte[100], 0, 100);
            }
        }

        [Test]
        public void Should_accept_connections_concurrently()
        {
            var connected = new ManualResetEvent(false);
            Task.Run(() =>
            {
                using (var invalidClient = new TcpClient())
                {
                    invalidClient.Connect(_uri.Host, _uri.Port);
                    connected.Set();
                    invalidClient.ReceiveTimeout = 10_000;
                    invalidClient.GetStream().Read(new byte[100], 0, 100);
                }
            });
            connected.WaitOne(TimeSpan.FromMilliseconds(500)).Should().BeTrue();
            Assert.DoesNotThrow(() =>
            {
                using (CreateClient())
                {
                }
            });
        }

        private static void AssertReceived(TcpClient client)
        {
            var expectedBytes = 87;
            var left = expectedBytes;
            var buffer = new byte[512];
            var received = 0;
            do
            {
                left -= client.GetStream().Read(buffer, 0, 84);
                received = expectedBytes - left;
            } while (left != 0);

            received.Should().BeGreaterOrEqualTo(84);
            Console.WriteLine(received);
            Console.WriteLine(BitConverter.ToString(buffer, 0, received));
        }


        private void OnCallback(Channel channel, in MessageHeader messageHeader, object data) => channel.Post(data);

        private TcpClient CreateClient()
        {
            var client = new AsyncTcpClient();
            if (!Debugger.IsAttached)
            {
                client.ReceiveTimeout = 5000;
            }

            client.Connect(_uri.Host, _uri.Port);
            client.Stream.SendHandshake(_clientHubId, CancellationToken.None);
            HandshakeMessage _ = client.Stream.ReceiveHandshake(CancellationToken.None).Result;
            return client;
        }
    }
}