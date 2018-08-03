using System.Text;
using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class PooledMemoryStreamTest
    {
        [Test]
        public void Should_be_able_to_write_and_read()
        {
            byte[] origin = Encoding.UTF8.GetBytes("Hello World!");

            PooledMemoryStream stream = PooledMemoryStream.Rent();
            stream.Write(origin, 0, origin.Length);
            stream.Complete();

            var actual = new byte[origin.Length];

            int readBytes = stream.Read(actual, 0, actual.Length);

            readBytes.Should().Be(origin.Length);
            actual.Should().BeEquivalentTo(origin);
        }
    }
}