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
        }

        [Test]
        public void Should_return_task_if_its_available()
        {
            _session.Enqueue(in _message);
            bool result = _session.TryDequeueSync(out MessageId messageId, out Message message);

            result.Should().BeTrue();
            message.Should().BeEquivalentTo(_message);
        }

        [Test]
        public async Task Should_wait_for_task_until_it_is_available()
        {
            FillSessionAsync();


            bool result = _session.TryDequeueSync(out MessageId messageId, out Message message);

            result.Should().BeFalse();

            (messageId, message) = await _session.DequeueAsync(CancellationToken.None);

            message.Should().BeEquivalentTo(_message);
        }


        private void FillSessionAsync() => Task.Run(() =>
        {
            Thread.Sleep(500);
            _session.Enqueue(in _message);
        });
    }
}