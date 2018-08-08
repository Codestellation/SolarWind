using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class ServerTests
    {
        private SolarWindHub _hub;
        private Uri _uri;
        private MemoryStream _messageBuffer;
        private Message _message;
        private HubId _clientHubId;

        [SetUp]
        public void Setup()
        {
            var options = new SolarWindHubOptions
            {
                Callback = OnCallback,
                Serializer = new JsonNetSerializer()
            };
            _hub = new SolarWindHub(options);
            _uri = new Uri("tcp://localhost:4312");
            _hub.Listen(_uri);


            var header = new MessageHeader(new MessageTypeId(1), MessageId.Empty);
            var data = new TextMessage {Text = "Greetings"};

            _messageBuffer = new MemoryStream();

            options.Serializer.SerializeMessage(_messageBuffer, in header, data);

            _clientHubId = new HubId("client");
        }

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
                for (var i = 0; i < 12; i++)
                {
                    client.Client.Send(_messageBuffer.GetBuffer(), i * chunkSize, chunkSize, SocketFlags.None);
                    Thread.Sleep(100);

                    if (i == 5)
                    {
                        client.Client.Disconnect(true);
                        client.Dispose();
                    }

                    break;
                }
            }

            Thread.Sleep(1000);

            using (TcpClient client = CreateClient())
            {
                client.Client.Send(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, SocketFlags.None);

                AssertReceived(client);
            }
        }

        private static void AssertReceived(TcpClient client)
        {
            var expectedBytes = 87;
            int left = expectedBytes;
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


        private void OnCallback(Channel channel, MessageHeader header, object data) => channel.Post(header.TypeId, data);

        private TcpClient CreateClient()
        {
            var client = new TcpClient();
            if (!Debugger.IsAttached)
            {
                client.ReceiveTimeout = 5000;
            }

            client.Connect(_uri.Host, _uri.Port);
            client.GetStream().SendHandshake(_clientHubId);
            HandshakeMessage _ = client.GetStream().ReceiveHandshake().Result;
            return client;
        }
    }
}