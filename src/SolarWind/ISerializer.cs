using System.IO;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Besides serialization and deserialization
    /// </summary>
    public interface ISerializer
    {
        MessageTypeId Serialize(object data, Stream stream);

        object Deserialize(MessageTypeId typeId, Stream stream);
    }
}