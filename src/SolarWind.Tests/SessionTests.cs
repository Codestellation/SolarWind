using System;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class SessionTests
    {
        private Session _session;
        private PooledMemoryStream _payload;
        private MessageTypeId _messageTypeId;

        [SetUp]
        public void Setup()
        {
            var channel = new Channel(new SolarWindHubOptions());
            _session = new Session(channel);
            _payload = PooledMemoryStream.Rent();
            _payload.CompleteWrite();
            _payload.CompleteRead();

            _messageTypeId = new MessageTypeId(1);
        }

        [TearDown]
        public void Teardown() => PooledMemoryStream.Return(_payload);

        [Test]
        public void Should_return_task_if_its_available()
        {
            MessageId id = _session.EnqueueOutgoing(_messageTypeId, _payload);

            bool result = _session.TryDequeueSync(out Message message);

            result.Should().BeTrue();

            message.Should().BeEquivalentTo(new Message(new MessageHeader(_messageTypeId, id), _payload));
        }

        [Test]
        public async Task Should_wait_for_task_until_it_is_available()
        {
            FillSessionAsync();

            bool result = _session.TryDequeueSync(out Message message);

            result.Should().BeFalse();

            message = await _session.DequeueAsync(CancellationToken.None);

            message.Should().BeEquivalentTo(new Message(new MessageHeader(_messageTypeId, new MessageId(1)), _payload));
        }


        private void FillSessionAsync() => Task.Run(() =>
        {
            Thread.Sleep(500);
            _session.EnqueueOutgoing(_messageTypeId, _payload);
        });
    }
}