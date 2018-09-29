using System;
using System.IO;
using System.Text;
using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    [TestFixture]
    public class DuplexBufferedStreamTests
    {
        Random _random;
        private MemoryStream _inner;
        private DuplexBufferedStream _stream;

        [SetUp]
        public void Setup()
        {
            _random = new Random();
            _inner = new MemoryStream();
            _stream = new DuplexBufferedStream(_inner, 16);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(15)]
        [TestCase(17)]
        [TestCase(256)]
        public void Should_write_all_the_buffered_data_correctly(int length)
        {
            var origin = WriteSomeBytes(_stream, length);

            _inner.Position = 0;
            (int read, byte[] actual) = ReadSomeBytes(_inner, length);


            read.Should().Be(length);
            actual.Should().BeEquivalentTo(origin);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(15)]
        [TestCase(17)]
        [TestCase(256)]
        public void Should_read_all_the_buffered_data_correctly(int length)
        {
            var origin = WriteSomeBytes(_inner, length);
            _inner.Position = 0;

            (int read, byte[] actual) = ReadSomeBytes(_stream, length);


            read.Should().Be(length);
            actual.Should().BeEquivalentTo(origin);
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
            stream.Flush();
            return origin;
        }

        private static (int bytesRead, byte[] actual) ReadSomeBytes(Stream stream, int length)
        {
            var actual = new byte[length];
            var totalRead = length;
            while (totalRead < length)
            {
                int readBytes = stream.Read(actual, totalRead, length - totalRead);
                if (readBytes == 0)
                {
                    break;
                }

                totalRead += readBytes;
            }
            
            return (totalRead, actual);
        }
    }
}