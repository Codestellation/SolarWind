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
        private readonly AsyncNetworkStream _asyncStream;


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
            _asyncStream = inner as AsyncNetworkStream;
            _readBuffer = new byte[bufferSize];
            _writeBuffer = new byte[bufferSize];
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
        {
            if (_asyncStream == null)
            {
                return _inner.ReadAsync(buffer, offset, count, cancellation);
            }

            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellation).AsTask();
        }

#if NETSTANDARD2_0
        public ValueTask<int> ReadAsync(Memory<byte> to, CancellationToken cancellation)
        {
            if (_asyncStream == null)
            {
                throw new NotImplementedException();
            }

            //TODO: buffer the data
            return _asyncStream.ReadAsync(to, cancellation);
        }
#endif
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
            //TODO: Here's bug:
            _readLen = _inner.Read(_readBuffer, 0, _readBuffer.Length);

            if (_readLen == 0) //eof in the inner stream
            {
                return 0;
            }

            return ReadBufferedData(to);
        }

        private int ReadBufferedData(Span<byte> to)
        {
            var bytesToRead = Math.Min(to.Length, _readLen - _readPos);
            var from = new ReadOnlySpan<byte>(_readBuffer, _readPos, bytesToRead);
            from.CopyTo(to);
            _readPos += bytesToRead;
            return from.Length;
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> from, CancellationToken cancellation)
        {
            if (_asyncStream == null)
            {
                throw new NotSupportedException();
            }

            var left = from.Length;
            var written = 0;
            do
            {
                Memory<byte> to = new Memory<byte>(_writeBuffer).Slice(_writePos);
                var bytesToWrite = Math.Min(left, to.Length);

                from.Slice(written, bytesToWrite).CopyTo(to);

                _writePos += bytesToWrite;
                written += bytesToWrite;
                left -= bytesToWrite;

                if (_writePos == _writeBuffer.Length)
                {
                    await FlushAsync(cancellation).ConfigureAwait(false);
                }
            } while (left != 0);
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlyMemory<byte>(buffer, offset, count));

        public void Write(ReadOnlyMemory<byte> from) => Write(from.Span);

        public void Write(ReadOnlySpan<byte> from)
        {
            var left = from.Length;
            var written = 0;
            do
            {
                Memory<byte> to = new Memory<byte>(_writeBuffer).Slice(_writePos);
                var bytesToWrite = Math.Min(left, to.Length);

                from
                    .Slice(written, bytesToWrite)
                    .CopyTo(to.Span);

                _writePos += bytesToWrite;
                written += bytesToWrite;
                left -= bytesToWrite;

                if (_writePos == _writeBuffer.Length)
                {
                    Flush();
                }
            } while (left != 0);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Close() => _inner.Close();

        protected override void Dispose(bool disposing) => _inner.Dispose();

        public override async Task FlushAsync(CancellationToken cancellation)
        {
            if (_writePos == 0)
            {
                return;
            }

            if (_asyncStream == null)
            {
                Task task = _inner.WriteAsync(_writeBuffer, 0, _writePos, cancellation);
                _writePos = 0;
                await task.ConfigureAwait(false);
                return;
            }

            var memory = new ReadOnlyMemory<byte>(_writeBuffer, 0, _writePos);
            await _asyncStream.WriteAsync(memory, cancellation).ConfigureAwait(false);
            _writePos = 0;
        }

        public override void Flush()
        {
            if (_writePos == 0)
            {
                return;
            }

            _inner.Write(_writeBuffer, 0, _writePos);
            _writePos = 0;
        }
    }
}