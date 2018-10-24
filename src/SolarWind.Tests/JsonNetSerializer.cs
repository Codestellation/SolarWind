using System.IO;
using System.Text;
using Codestellation.SolarWind.Protocol;
using Newtonsoft.Json;

namespace Codestellation.SolarWind.Tests
{
    public class JsonNetSerializer : ISerializer
    {
        public static readonly JsonNetSerializer Instance = new JsonNetSerializer();

        private readonly JsonSerializer _serializer;

        public JsonNetSerializer()
        {
            _serializer = new JsonSerializer();
        }

        public MessageTypeId Serialize(object data, Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 128, true))
            {
                _serializer.Serialize(writer, data);
            }

            return new MessageTypeId(1);
        }

        public object Deserialize(in MessageHeader header, Stream stream)
        {
            using (var reader = new JsonTextReader(new StreamReader(stream)) {CloseInput = false})
            {
                return _serializer.Deserialize<TextMessage>(reader);
            }
        }
    }
}