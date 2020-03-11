using System;
using System.Buffers;
using System.IO;
using Codestellation.SolarWind;
using Codestellation.SolarWind.Protocol;

namespace Benchmark
{
    public class NullSerializer : ISerializer
    {
        public static readonly NullSerializer Instance = new NullSerializer();
        public static readonly object Dummy = new object();
        private readonly byte[] _dumbData;
        private readonly int MaxDataLength;


        private NullSerializer()
        {
            MaxDataLength = 16 * 1024;
            var dumbData = new byte[MaxDataLength];
            var random = new Random();
            for (var i = 0; i < dumbData.Length; i++)
            {
                dumbData[i] = (byte)random.Next(1, byte.MaxValue);
            }


            _dumbData = dumbData;
        }

        public MessageTypeId Serialize(object data, Stream stream)
        {
            var length = data.GetHashCode() % MaxDataLength;
            if (length < 0)
            {
                length = -length;
            }

            stream.Write(_dumbData, 0, length);
            return new MessageTypeId(1);
        }

        public object Deserialize(in MessageHeader typeId, Stream stream)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)stream.Length);
            stream.Read(buffer, 0, (int)stream.Length);
            ArrayPool<byte>.Shared.Return(buffer);
            return Dummy;
        }
    }
}