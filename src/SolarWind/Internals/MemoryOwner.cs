using System;
using System.Buffers;
using System.IO.Pipelines;

namespace Codestellation.SolarWind.Internals
{
    public class MemoryOwner
    {
        private static readonly ObjectPool<MemoryOwner> Pool = new ObjectPool<MemoryOwner>(() => new MemoryOwner(), 1024);

        private readonly Pipe _pipe;

        private PipeWriter Writer => _pipe.Writer;
        private PipeReader Reader => _pipe.Reader;

        private MemoryOwner()
        {
            _pipe = new Pipe();
        }

        public void Complete() => _pipe.Writer.Complete();

        public static MemoryOwner Rent() => Pool.Rent();

        public static void Return(MemoryOwner owner) => Pool.Return(owner);


        public void Write(byte[] buffer, int offset, int count) => WriteImpl(new ReadOnlySpan<byte>(buffer, offset, count));

        private void WriteImpl(ReadOnlySpan<byte> from)
        {
            var offset = 0;
            int count = from.Length;
            while (count > 0)
            {
                Span<byte> to = Writer.GetSpan(1);
                int bytes = Math.Min(count, to.Length);
                from.Slice(offset, bytes).CopyTo(to.Slice(0, bytes));
                Writer.Advance(bytes);
                offset += bytes;
                count -= bytes;
            }
        }

        public int Read(Memory<byte> to)
        {
            if (to.IsEmpty)
            {
                return 0;
            }

            if (!Reader.TryRead(out ReadResult from))
            {
                return 0;
            }

            return ConsumeBytes(from, to.Span);
        }

        private int ConsumeBytes(ReadResult from, Span<byte> to)
        {
            var bytesRead = 0;
            if (!from.IsCanceled)
            {
                ReadOnlySequence<byte> buffer = from.Buffer;
                int remaining = to.Length;
                if (buffer.IsSingleSegment)
                {
                    ReadOnlySpan<byte> segSpan = buffer.First.Span;
                    bytesRead = Math.Min(segSpan.Length, remaining);
                    segSpan.Slice(0, bytesRead).CopyTo(to);
                }
                else
                {
                    if (remaining != 0)
                    {
                        foreach (ReadOnlyMemory<byte> segment in buffer)
                        {
                            ReadOnlySpan<byte> segSpan = segment.Span;
                            int take = Math.Min(segSpan.Length, remaining);
                            segSpan.Slice(0, take).CopyTo(to);
                            to = to.Slice(take);
                            bytesRead += take;
                            remaining -= take;
                        }
                    }
                }

                SequencePosition end = buffer.GetPosition(bytesRead);
                Reader.AdvanceTo(end);
            }

            return bytesRead;
        }
    }
}