using System.Runtime.InteropServices;

namespace Codestellation.SolarWind
{
    //Structure has some reserved space for the future
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Header
    {
        public static readonly unsafe int Size = sizeof(Header);

        public readonly ushort Version;
        public readonly MessageTypeId MessageId;
        public readonly PayloadSize PayloadSize;

        public Header(MessageTypeId messageId, PayloadSize payloadSize)
        {
            Version = 1;
            MessageId = messageId;
            PayloadSize = payloadSize;
        }

        public static unsafe void WriteTo(in Header header, byte[] buffer)
        {
            fixed (byte* p = buffer)
            {
                *((Header*)p) = header;
            }
        }

        public static unsafe ref Header ReadFrom(byte[] buffer)
        {
            fixed (byte* p = buffer)
            {
                return ref *((Header*)p);
            }
        }
    }
}