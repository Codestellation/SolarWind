using System.Collections.Concurrent;

namespace Codestellation.SolarWind
{
    public class SolarWindServer
    {
        private readonly ConcurrentDictionary<MessageId, object> _requestRegistry;
        public SolarWindServer()
        {
            _requestRegistry = new ConcurrentDictionary<MessageId, object>();
        }
    }
}