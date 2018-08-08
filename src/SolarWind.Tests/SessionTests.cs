using Codestellation.SolarWind.Internals;
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
    }
}