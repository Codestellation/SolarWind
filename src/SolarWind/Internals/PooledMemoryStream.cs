using System;
using System.IO;

namespace Codestellation.SolarWind.Internals
{
    public class PooledMemoryStream : Stream
    {
        private static readonly ObjectPool<PooledMemoryStream> Pool = new ObjectPool<PooledMemoryStream>(() => new PooledMemoryStream(), 1024);

        public static PooledMemoryStream Rent()
        {
            PooledMemoryStream pooledMemoryStream = Pool.Rent();
            pooledMemoryStream.Init();
            return pooledMemoryStream;
        }

        private void Init() => _memory = MemoryOwner.Rent();

        private PooledMemoryStream()
        {
        }

        private MemoryOwner _memory;
        private long _length;

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

        public override void Write(byte[] buffer, int offset, int count) => _memory.Write(buffer, offset, count);


        public override int ReadByte() => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => _memory.Read(new Memory<byte>(buffer, offset, count));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _length = 0;
            _memory = null;
            Pool.Return(this);
        }

        public void Complete() => _memory.Complete();
    }
}