using System;
using System.IO.Pipelines;

namespace Codestellation.SolarWind
{
    public readonly unsafe struct MessageTypeId
    {
        public readonly long Id;

        public MessageTypeId(long id)
        {
            Id = id;
        }

        public void WriteTo(Span<byte> span)
        {
            fixed (byte* p = span)
            {
                *((long*)p) = Id;
            }
        }

        //TODO: fix that later
        public static MessageTypeId ReadFrom(in ReadResult result) => new MessageTypeId(1);
    }
}