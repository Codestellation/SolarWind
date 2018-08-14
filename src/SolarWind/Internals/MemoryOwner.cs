using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    public class MemoryOwner
    {
        private readonly Pipe _pipe;

        private PipeWriter Writer => _pipe.Writer;
        private PipeReader Reader => _pipe.Reader;

        public MemoryOwner()
        {
            _pipe = new Pipe();
        }

        public void CompleteWrite() => Writer.Complete();

        //TODO: Consider providing an exception object
        public void CompleteRead() => Reader.Complete();

        public void Reset() => _pipe.Reset();

        public void Write(in ReadOnlySpan<byte> buffer) => WriteImpl(buffer);

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

        internal int WriteFrom(Stream source, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            var readBytes = 0;
            int left = count;
            do
            {
                Memory<byte> to = Writer.GetMemory(1);
                if (MemoryMarshal.TryGetArray(to, out ArraySegment<byte> buffer))
                {
                    int bytesToRead = Math.Min(left, buffer.Count - buffer.Offset);
                    readBytes = source.Read(buffer.Array, buffer.Offset, bytesToRead);
                    left -= readBytes;
                    Writer.Advance(readBytes);
                }
                else
                {
                    ThrowMemoryIsNotAnArray();
                }
            } while (left > 0 && readBytes > 0);

            return count - left;
        }

        internal async Task<int> WriteFromAsync(Stream source, int count, CancellationToken cancellation)
        {
            if (count == 0)
            {
                return 0;
            }

            var readBytes = 0;
            int left = count;
            do
            {
                Memory<byte> to = Writer.GetMemory(1);
                if (MemoryMarshal.TryGetArray(to, out ArraySegment<byte> buffer))
                {
                    int bytesToRead = Math.Min(left, buffer.Count - buffer.Offset);
                    readBytes = await source
                        .ReadAsync(buffer.Array, buffer.Offset, bytesToRead, cancellation)
                        .ConfigureAwait(false);
                    left -= readBytes;
                    Writer.Advance(readBytes);
                }
                else
                {
                    ThrowMemoryIsNotAnArray();
                }
            } while (left > 0 && readBytes > 0);

            return count - left;
        }

        public int Read(Memory<byte> to) => Read(to.Span);

        public int Read(Span<byte> to)
        {
            if (to.IsEmpty)
            {
                return 0;
            }

            if (!Reader.TryRead(out ReadResult from))
            {
                return 0;
            }

            return ConsumeBytes(from, to);
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

        public void CopyTo(Stream stream)
        {
            if (!Reader.TryRead(out ReadResult from))
            {
                return;
            }


            var bytesRead = 0;
            if (!from.IsCanceled)
            {
                ReadOnlySequence<byte> buffer = from.Buffer;

                foreach (ReadOnlyMemory<byte> segment in buffer)
                {
                    if (!MemoryMarshal.TryGetArray(segment, out ArraySegment<byte> array))
                    {
                        ThrowMemoryIsNotAnArray();
                    }

                    stream.Write(array.Array, array.Offset, array.Count);
                    bytesRead += array.Count;

                    //Free buffers asap
                    SequencePosition end = buffer.GetPosition(bytesRead);
                    Reader.AdvanceTo(end);
                }
            }
        }

        public async Task CopyToAsync(Stream stream, CancellationToken cancellation)
        {
            if (!Reader.TryRead(out ReadResult from))
            {
                return;
            }


            var bytesRead = 0;
            if (!from.IsCanceled)
            {
                ReadOnlySequence<byte> buffer = from.Buffer;

                foreach (ReadOnlyMemory<byte> segment in buffer)
                {
                    if (!MemoryMarshal.TryGetArray(segment, out ArraySegment<byte> array))
                    {
                        ThrowMemoryIsNotAnArray();
                    }

                    await stream.WriteAsync(array.Array, array.Offset, array.Count, cancellation).ConfigureAwait(false);
                    bytesRead += array.Count;

                    //Free buffers asap
                    SequencePosition end = buffer.GetPosition(bytesRead);
                    Reader.AdvanceTo(end);
                }
            }
        }

        private static void ThrowMemoryIsNotAnArray() => throw new NotSupportedException("Memory pools with non-array based memory are not supported currently.");
    }
}