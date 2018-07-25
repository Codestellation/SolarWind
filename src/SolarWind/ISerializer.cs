using System.IO;

namespace Codestellation.SolarWind
{
    public interface ISerializer
    {
        void Serialize(object data, Stream stream);

        object Deserialize(MessageTypeId prefix, Stream stream);
    }
}