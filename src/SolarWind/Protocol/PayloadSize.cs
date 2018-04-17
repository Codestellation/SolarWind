using System.IO;

namespace Codestellation.SolarWind.Protocol
{
    public struct PayloadSize
    {
        public readonly int Value;

        public PayloadSize(int value)
        {
            Value = value;
        }

        public static PayloadSize From(Stream stream, int offset) => new PayloadSize((int)(stream.Position - offset));

        public static bool operator <=(PayloadSize size, long value) => size.Value <= value;

        public static bool operator >=(PayloadSize size, long value) => size.Value >= value;

        public static bool operator <(PayloadSize size, long value) => size.Value < value;

        public static bool operator >(PayloadSize size, long value) => size.Value > value;


        public static bool operator <=(PayloadSize size, int value) => size.Value <= value;

        public static bool operator >=(PayloadSize size, int value) => size.Value >= value;

        public static bool operator <(PayloadSize size, int value) => size.Value < value;

        public static bool operator >(PayloadSize size, int value) => size.Value > value;


        public static bool operator <=(int value, PayloadSize size) => value <= size.Value;

        public static bool operator >=(int value, PayloadSize size) => value >= size.Value;

        public static bool operator <(int value, PayloadSize size) => value < size.Value;

        public static bool operator >(int value, PayloadSize size) => value > size.Value;
    }
}