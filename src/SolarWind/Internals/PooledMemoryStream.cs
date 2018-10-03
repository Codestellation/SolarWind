using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    //TODO: Made it internal to avoid exposing it for users
    public class PooledMemoryStream : Stream
    {
        private const int MinBufferSize = 128;

        private long _length;
        private long _position;
        private readonly LinkedList<byte[]> _buffers;

        public override bool CanRead { get; } = true;

        public override bool CanWrite { get; } = true;

        public override bool CanSeek { get; } = false;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public void Reset()
        {
            _length = 0;
            _position = 0;
        }

        public PooledMemoryStream()
        {
            _buffers = new LinkedList<byte[]>();
        }

        public override void Flush()
        {
        }

        public override void WriteByte(byte value)
        {
            ReadOnlySpan<byte> from = stackalloc byte[] {value};
            Write(from);
        }

        public void Write(ReadOnlySpan<byte> from)
        {
            int left = from.Length;
            var start = 0;
            while (left != 0)
            {
                Memory<byte> memory = GetWritableMemory(left);
                int length = Math.Min(left, memory.Length);
                from
                    .Slice(start, length)
                    .CopyTo(memory.Span);

                left -= length;
                _position += length;
                start += length;
            }

            if (_length < _position)
            {
                _length = _position;
            }
        }

        //TODO: take into account request size in case of allocation buffers. 
        private Memory<byte> GetWritableMemory(int requested)
        {
            if (_buffers.Count == 0)
            {
                return RentNew(requested);
            }

            var start = (int)_position;

            foreach (byte[] buffer in _buffers)
            {
                int tailLength = buffer.Length - start;
                if (tailLength > 0)
                {
                    int length = Math.Min(requested, tailLength);
                    return new Memory<byte>(buffer, start, length);
                }

                start -= buffer.Length;
            }

            return RentNew(requested);
        }

        private Memory<byte> RentNew(int requested)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BytesToRent);
            _buffers.AddLast(buffer);
            int length = Math.Min(requested, buffer.Length);
            return new Memory<byte>(buffer, 0, length);
        }

        private int BytesToRent => MinBufferSize << _buffers.Count;

        public override void Write(byte[] buffer, int offset, int count) => Write(new Span<byte>(buffer, offset, count));

        public override unsafe int ReadByte()
        {
            Span<byte> to = stackalloc byte[1];

            return Read(to) == 0 ? -1 : to[0];
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(new Span<byte>(buffer, offset, count));

        public int Read(Span<byte> to)
        {
            if (to.Length == 0)
            {
                return 0;
            }

            int left = to.Length;
            Span<byte> currentTo = to;
            while (left != 0 && TryGetReadableSpan(left, out ReadOnlySpan<byte> from))
            {
                from.CopyTo(currentTo);
                left -= from.Length;
                currentTo = currentTo.Slice(from.Length);
            }

            return to.Length - left;
        }

        private bool TryGetReadableSpan(int requested, out ReadOnlySpan<byte> from)
        {
            if (_length == _position)
            {
                from = default;
                return false;
            }

            var start = (int)_position;
            var totalNotRead = (int)(_length - _position);
            int maxToRead = Math.Min(totalNotRead, requested);

            foreach (byte[] buffer in _buffers)
            {
                if (buffer.Length - start > 0)
                {
                    //So we found a buffer to read. 
                    int bufferTail = buffer.Length - start;
                    int spanLength = Math.Min(bufferTail, maxToRead);
                    from = new ReadOnlySpan<byte>(buffer, start, spanLength);

                    _position += from.Length;
                    return true;
                }

                start -= buffer.Length;
            }

            from = default;
            return false;
        }


        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReturnBuffers();
                GC.SuppressFinalize(this);
            }
        }

        ~PooledMemoryStream()
        {
            ReturnBuffers();
        }

        private void ReturnBuffers()
        {
            foreach (byte[] buffer in _buffers)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            _buffers.Clear();
        }

        public int Write(Stream from, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            var lastRead = 0;
            int left = count;

            do
            {
                MemoryMarshal.TryGetArray(GetWritableMemory(left), out ArraySegment<byte> segment);
                int bytesToRead = Math.Min(count, segment.Count);
                lastRead = from.Read(segment.Array, segment.Offset, bytesToRead);
                left -= lastRead;
                _position += lastRead;
            } while (left != 0 && lastRead > 0);

            if (_position > _length)
            {
                _length = _position;
            }

            return count - left;
        }

        public ValueTask<int> WriteAsync(Stream from, int count, CancellationToken cancellation) => new ValueTask<int>(Write(from, count));

        public ValueTask CopyIntoAsync(Stream destination, CancellationToken cancellation)
        {
            CopyInto(destination);
            return new ValueTask(Task.CompletedTask);
        }

        public async ValueTask CopyIntoAsync(AsyncNetworkStream destination, CancellationToken cancellation)
        {
            var left = (int)_length;
            foreach (byte[] buffer in _buffers)
            {
                int bytesToCopy = Math.Min(left, buffer.Length);
                var memory = new ReadOnlyMemory<byte>(buffer, 0, bytesToCopy);
                await destination.WriteAsync(memory, cancellation);
                left -= bytesToCopy;
                if (left == 0)
                {
                    break;
                }
            }
        }

        public async ValueTask CopyIntoAsync(DuplexBufferedStream destination, CancellationToken cancellation)
        {
            var left = (int)_length;
            foreach (byte[] buffer in _buffers)
            {
                int bytesToCopy = Math.Min(left, buffer.Length);
                var memory = new ReadOnlyMemory<byte>(buffer, 0, bytesToCopy);
                await destination.WriteAsync(memory, cancellation);
                left -= bytesToCopy;
                if (left == 0)
                {
                    break;
                }
            }
        }

        public void CopyInto(Stream destination)
        {
            var left = (int)_length;
            foreach (byte[] buffer in _buffers)
            {
                int bytesToCopy = Math.Min(left, buffer.Length);
                destination.Write(buffer, 0, bytesToCopy);
                left -= bytesToCopy;
                if (left == 0)
                {
                    break;
                }
            }
        }
    }
}