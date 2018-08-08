using System.IO;
using System.Text;
using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class PooledMemoryStreamTest
    {
        private PooledMemoryStream _stream;

        [SetUp]
        public void Setup() => _stream = PooledMemoryStream.Rent();

        [TearDown]
        public void Teardown() => PooledMemoryStream.Return(_stream);

        [Test]
        public void Should_be_able_to_write_and_read()
        {
            byte[] origin = Encoding.UTF8.GetBytes("Hello World!");
            _stream.Write(origin, 0, origin.Length);
            _stream.CompleteWrite();

            var actual = new byte[origin.Length];

            int readBytes = _stream.Read(actual, 0, actual.Length);

            _stream.CompleteRead();

            readBytes.Should().Be(origin.Length);
            actual.Should().BeEquivalentTo(origin);
        }


        [Test]
        public void Should_be_able_to_read_from_stream()
        {
            byte[] origin = Encoding.UTF8.GetBytes("Hello World!");
            var memory = new MemoryStream();
            memory.Write(origin, 0, origin.Length);
            memory.Position = 0;
            _stream.WriteFrom(memory, origin.Length);
            _stream.CompleteWrite();

            var actual = new byte[origin.Length];

            int readBytes = _stream.Read(actual, 0, actual.Length);

            _stream.CompleteRead();

            readBytes.Should().Be(origin.Length);
            actual.Should().BeEquivalentTo(origin);
        }
    }
}