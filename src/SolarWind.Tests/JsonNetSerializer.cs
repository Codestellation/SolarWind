using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Codestellation.SolarWind.Tests
{
    public class JsonNetSerializer : ISerializer
    {
        private readonly JsonSerializer _serializer;

        public JsonNetSerializer()
        {
            _serializer = new JsonSerializer();
        }

        public void Serialize(object data, Stream stream)
        {
            using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
            {
                _serializer.Serialize(writer, data);
            }
        }

        public object Deserialize(MessageTypeId prefix, Stream stream) => throw new NotImplementedException();
    }
}