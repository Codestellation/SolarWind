using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    //TODO: Made it internal to avoid exposing it for users
    public class PooledMemoryStream : Stream
    {
        private static readonly ObjectPool<PooledMemoryStream> Pool = new ObjectPool<PooledMemoryStream>(() => new PooledMemoryStream(), 1024);

        public static PooledMemoryStream Rent()
        {
            PooledMemoryStream pooledMemoryStream = Pool.Rent();
            if (pooledMemoryStream.Length > 0)
            {
                throw new InvalidOperationException("Stream was not cleaned gracefully");
            }

            return pooledMemoryStream;
        }

        public static void Return(PooledMemoryStream stream)
        {
            stream.Reset();
            Pool.Return(stream);
        }

        public static void ResetAndReturn(PooledMemoryStream stream)
        {
            stream.CompleteWrite();
            stream.CompleteRead();
            Return(stream);
        }

        public void Reset()
        {
            _length = 0;
            _memory.Reset();
        }

        private readonly MemoryOwner _memory;

        private long _length;

        private PooledMemoryStream()
        {
            _memory = new MemoryOwner();
        }

        public override bool CanRead { get; } = true;

        public override bool CanWrite { get; } = true;

        public override bool CanSeek { get; } = false;

        public override long Length => _length;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public void Write(Memory<byte> from) => Write(from.Span);

        public void Write(Span<byte> from)
        {
            _memory.Write(from);
            _length += from.Length;
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(new Span<byte>(buffer, offset, count));


        public int WriteFrom(Stream from, int length)
        {
            int written = _memory.WriteFrom(from, length);
            _length += written;
            return written;
        }

        public async ValueTask<int> WriteFromAsync(AsyncNetworkStream from, int length, CancellationToken cancellation)
        {
            int written = await _memory
                .WriteFromAsync(from, length, cancellation)
                .ConfigureAwait(false);
            _length += written;
            return written;
        }

        public override int ReadByte() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => _memory.Read(new Memory<byte>(buffer, offset, count));

        public int Read(in Span<byte> buffer) => _memory.Read(buffer);

        public void CopyInto(Stream stream) => _memory.CopyTo(stream);
        public ValueTask CopyIntoAsync(AsyncNetworkStream stream, CancellationToken cancellation) => _memory.CopyToAsync(stream, cancellation);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
        }


        public void CompleteWrite() => _memory.CompleteWrite();

        public void CompleteRead() => _memory.CompleteRead();
    }
}