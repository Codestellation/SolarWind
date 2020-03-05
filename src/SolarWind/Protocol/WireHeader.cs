using System;
using System.Runtime.InteropServices;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind.Protocol
{
    //Structure has some reserved space for the future
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct WireHeader
    {
        public static readonly unsafe int Size = sizeof(WireHeader);

        public readonly ushort Version;
        public readonly MessageHeader MessageHeader;
        public readonly PayloadSize PayloadSize;


        public bool IsHandshake => MessageHeader.TypeId == MessageTypeId.Handshake;

        public WireHeader(MessageHeader messageHeader, PayloadSize payloadSize)
        {
            Version = 1;
            MessageHeader = messageHeader;
            PayloadSize = payloadSize;
        }

        public static unsafe void WriteTo(in WireHeader wireHeader, byte[] buffer, int startIndex)
        {
            fixed (byte* p = buffer)
            {
                *(WireHeader*)(p + startIndex) = wireHeader;
            }
        }

        public static unsafe void WriteTo(in WireHeader wireHeader, PooledMemoryStream buffer)
        {
            fixed (WireHeader* wh = &wireHeader)
            {
                var span = new Span<byte>(wh, Size);
                buffer.Write(span);
            }
        }

        public static unsafe WireHeader ReadFrom(PooledMemoryStream buffer)
        {
            Span<byte> span = stackalloc byte[Size];
            buffer.Read(span);
            fixed (byte* p = span)
            {
                return *(WireHeader*)p;
            }
        }

        public static unsafe ref WireHeader ReadFrom(byte[] buffer)
        {
            fixed (byte* p = buffer)
            {
                return ref *(WireHeader*)p;
            }
        }
    }
}