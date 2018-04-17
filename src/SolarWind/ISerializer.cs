using System.IO;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Besides serialization and deserialization
    /// </summary>
    public interface ISerializer
    {
        MessageTypeId Serialize(object data, Stream stream);

        object Deserialize(in MessageHeader typeId, Stream stream);
    }
}