using System;
using System.IO;
using System.Text;
using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture(0)]
    [TestFixture(1)]
    [TestFixture(12)]
    [TestFixture(128)]
    [TestFixture(16000)]
    [TestFixture(1024 * 1024)]
    public class PooledMemoryStreamTest
    {
        private readonly Random _random;
        private readonly int _dataSize;

        public PooledMemoryStreamTest(int dataSize)
        {
            _random = new Random();
            _dataSize = dataSize;
        }

        [Test]
        public void Should_return_zero_read_bytes_if_eof()
        {
            using (var stream = new PooledMemoryStream())
            {
                byte[] origin = WriteSomeBytes(stream, _dataSize);

                (int bytesRead, _) = ReadSomeBytes(stream, origin.Length);

                stream.Position.Should().Be(origin.Length);
                bytesRead.Should().Be(0);
            }
        }

        [Test]
        public void Should_return_zero_read_bytes_if_eof2()
        {
            using (var stream = new PooledMemoryStream())
            {
                byte[] origin = WriteSomeBytes(stream, _dataSize);

                (int bytesRead, _) = ReadSomeBytes(stream, origin.Length + 10);

                stream.Position.Should().Be(origin.Length);
                bytesRead.Should().Be(0);
            }
        }


        [Test]
        public void Should_be_able_to_write_and_read()
        {
            using (var stream = new PooledMemoryStream())
            {
                byte[] origin = WriteSomeBytes(stream, _dataSize);
                stream.Position = 0;
                (int bytesRead, byte[] actual) = ReadSomeBytes(stream, origin.Length);

                bytesRead.Should().Be(origin.Length);
                stream.Position.Should().Be(origin.Length);
                actual.Should().BeEquivalentTo(origin);
            }
        }

        [Test]
        public void Should_be_able_to_read_from_stream()
        {
            using (var stream = new PooledMemoryStream())
            {
                var memory = new MemoryStream();
                byte[] origin = WriteSomeBytes(memory, _dataSize);
                memory.Position = 0;

                stream.Write(memory, origin.Length);

                var actual = new byte[origin.Length];

                stream.Position = 0;
                int readBytes = stream.Read(actual, 0, actual.Length);


                readBytes.Should().Be(origin.Length);
                actual.Should().BeEquivalentTo(origin);
            }
        }

        private static (int bytesRead, byte[] actual) ReadSomeBytes(PooledMemoryStream stream, int length)
        {
            var actual = new byte[length];
            int readBytes = stream.Read(actual, 0, actual.Length);
            return (readBytes, actual);
        }

        private byte[] WriteSomeBytes(Stream stream, int count)
        {
            var chars = new char[count];
            for (var i = 0; i < count; i++)
            {
                chars[i] = (char)_random.Next('a', 'z');
            }

            byte[] origin = Encoding.UTF8.GetBytes(chars);
            stream.Write(origin, 0, origin.Length);
            return origin;
        }
    }
}