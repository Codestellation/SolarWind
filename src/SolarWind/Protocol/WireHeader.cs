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

        public static unsafe void WriteTo(in WireHeader wireHeader, byte[] buffer)
        {
            fixed (byte* p = buffer)
            {
                *((WireHeader*)p) = wireHeader;
            }
        }

        public static unsafe void WriteTo(in WireHeader wireHeader, PooledMemoryStream buffer)
        {
            Span<byte> span = stackalloc byte[Size];
            fixed (byte* p = span)
            {
                *((WireHeader*)p) = wireHeader;
            }

            buffer.Write(span);
        }

        public static unsafe ref WireHeader ReadFrom(PooledMemoryStream buffer)
        {
            Span<byte> span = stackalloc byte[Size];
            buffer.Read(span);
            fixed (byte* p = span)
            {
                return ref *((WireHeader*)p);
            }
        }

        public static unsafe ref WireHeader ReadFrom(byte[] buffer)
        {
            fixed (byte* p = buffer)
            {
                return ref *((WireHeader*)p);
            }
        }
    }
}