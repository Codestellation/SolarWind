using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    //Thread safe to perform reads and writes simultaneously
    public class DuplexBufferedStream : Stream
    {
        private readonly Stream _inner;
        private readonly byte[] _readBuffer;
        private int _readPos;
        private int _readLen;

        private readonly byte[] _writeBuffer;
        private int _writePos;


        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }


        public DuplexBufferedStream(Stream inner, int bufferSize = 64 * 1024) //default size of socket buffers
        {
            _inner = inner;
            _readBuffer = new byte[bufferSize];
            _writeBuffer = new byte[bufferSize];
        }

        public override void Flush() => InternalFlush();

        private void InternalFlush()
        {
            if (_writePos == 0)
            {
                return;
            }

            _inner.Write(_writeBuffer, 0, _writePos);
            _writePos = 0;
        }


        public override int Read(byte[] buffer, int offset, int count) => Read(new Span<byte>(buffer, offset, count));

        public int Read(Span<byte> to)
        {
            //Some data is already buffered
            if (_readPos < _readLen)
            {
                return ReadBufferedData(to);
            }

            _readPos = 0;
            _readLen = 0;

            _readLen = _inner.Read(_readBuffer, 0, _readBuffer.Length);
            if (_readLen == 0) //eof in the inner stream
            {
                return 0;
            }

            return ReadBufferedData(to);
        }

        private int ReadBufferedData(Span<byte> to)
        {
            int bytesToRead = Math.Min(to.Length, _readLen - _readPos);
            var from = new ReadOnlySpan<byte>(_readBuffer, _readPos, bytesToRead);
            from.CopyTo(to);
            _readPos += bytesToRead;
            return from.Length;
        }


        public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));

        public void Write(ReadOnlySpan<byte> from)
        {
            int left = from.Length;
            var written = 0;
            do
            {
                Span<byte> to = new Span<byte>(_writeBuffer).Slice(_writePos);
                int bytesToWrite = Math.Min(left, to.Length);

                from.Slice(written, bytesToWrite).CopyTo(to);

                _writePos += bytesToWrite;
                written += bytesToWrite;
                left -= bytesToWrite;

                if (_writePos == _writeBuffer.Length)
                {
                    InternalFlush();
                }
            } while (left != 0);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Close() => _inner.Close();

        protected override void Dispose(bool disposing) => _inner.Dispose();

        public override Task FlushAsync(CancellationToken cancellation)
        {
            if (_writePos == 0)
            {
                return Task.CompletedTask;
            }

            Task task = _inner.WriteAsync(_writeBuffer, 0, _writePos, cancellation);
            _writePos = 0;
            return task;
        }
    }
}