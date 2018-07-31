using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class SessionTests
    {
        private Session _session;
        private Message _message;

        [SetUp]
        public void Setup()
        {
            _session = new Session();
            _message = new Message(new MessageTypeId(1), new object());
            _session.Enqueue(in _message);
        }

        [Test]
        public void Should_return_task_if_its_available()
        {
            ValueTask<(MessageId, Message)> dequeued = _session.Dequeue(CancellationToken.None);

            AssertDequeueResult(dequeued);
        }

        [Test]
        public async Task Should_wait_for_task_until_it_is_available()
        {
            FillSessionAsync();

            ValueTask<(MessageId, Message)> dequeued = _session.Dequeue(CancellationToken.None);

            await dequeued;

            AssertDequeueResult(dequeued);
        }

        private void AssertDequeueResult(ValueTask<(MessageId, Message)> dequeued)
        {
            dequeued.IsCompletedSuccessfully.Should().BeTrue();
            dequeued.Result.Item2.Should().BeEquivalentTo(_message);
        }

        private void FillSessionAsync() => Task.Run(() =>
        {
            Task.Delay(500);
            _session.Enqueue(in _message);
        });
    }
}