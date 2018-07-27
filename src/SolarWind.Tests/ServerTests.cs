using System;
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

            _message = new Message(new MessageTypeId(1), new TextMessage {Text = "Greetings"});
            _messageBuffer = new MemoryStream();

            options.Serializer.SerializeMessage(_messageBuffer, in _message);
        }

        [TearDown]
        public void TearDown() => _hub.Dispose();

        [Test]
        public void Should_respond_to_single_packet_message()
        {
            using (var client = new TcpClient {ReceiveTimeout = 5000})
            {
                client.Connect(_uri.Host, _uri.Port);
                client.Client.Send(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, SocketFlags.None);

                AssertReceived(client);
            }
        }

        [Test]
        public void Should_respond_to_multi_packet_message()
        {
            using (var client = new TcpClient {ReceiveTimeout = 5000})
            {
                client.Connect(_uri.Host, _uri.Port);

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
            using (var client = new TcpClient {ReceiveTimeout = 5000})
            {
                client.Connect(_uri.Host, _uri.Port);

                const int chunkSize = 7;
                for (var i = 0; i < 12; i++)
                {
                    client.Client.Send(_messageBuffer.GetBuffer(), i * chunkSize, chunkSize, SocketFlags.None);
                    Thread.Sleep(100);

                    if (i == 5)
                    {
                        client.Dispose();
                    }

                    break;
                }
            }


            using (var client = new TcpClient {ReceiveTimeout = 5000})
            {
                client.Connect(_uri.Host, _uri.Port);
                client.Client.Send(_messageBuffer.GetBuffer(), 0, (int)_messageBuffer.Position, SocketFlags.None);

                AssertReceived(client);
            }
        }

        private static void AssertReceived(TcpClient client)
        {
            var buffer = new byte[512];
            var received = client.Client.Receive(buffer);

            received.Should().BeGreaterOrEqualTo(84);
            Console.WriteLine(received);
            Console.WriteLine(BitConverter.ToString(buffer, 0, received));
        }


        private void OnCallback(Channel channel, Message message) => channel.Post(message);
    }
}