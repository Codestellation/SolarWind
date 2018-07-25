using System;
using System.Net.Sockets;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    public class PingPongTests
    {
        public class TextMessage
        {
            public string Text { get; set; }
        }

        [Test]
        public void PingPong()
        {
            var options = new SolarWindHubOptions
            {
                Callback = OnCallback,
                Serializer = new JsonNetSerializer(),
                OnAccept = OnAccept
            };
            var hub = new SolarWindHub(options);
            var uri = new Uri("tcp://localhost:4312");
            hub.Listen(uri);

            var client = new TcpClient();
            client.Connect(uri.Host, uri.Port);
            var buffer = new byte[512];

            var received = client.Client.Receive(buffer);

            received.Should().BeGreaterThan(0);
            Console.WriteLine(received);
            Console.WriteLine(BitConverter.ToString(buffer, 0, received));
        }

        private void OnCallback(Message message) => throw new NotImplementedException();

        private void OnAccept(Channel channel)
        {
            var data = new TextMessage {Text = "Hello, Hub!"};
            var message = new Message(new MessageTypeId(1), data);
            channel.Post(message);
        }
    }
}