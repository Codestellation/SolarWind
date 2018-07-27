using System.Runtime.InteropServices;

namespace Codestellation.SolarWind
{
    //Structure has some reserved space for the future
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct Header
    {
        public static readonly unsafe int Size = sizeof(Header);

        public readonly ushort Version;
        public readonly MessageTypeId MessageTypeId;
        public readonly PayloadSize PayloadSize;
        public readonly MessageId MessageId;


        public Header(MessageTypeId messageTypeTypeId, PayloadSize payloadSize, MessageId messageId = default)
        {
            Version = 1;
            MessageTypeId = messageTypeTypeId;
            PayloadSize = payloadSize;
            MessageId = messageId;
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