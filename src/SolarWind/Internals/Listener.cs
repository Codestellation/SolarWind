using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal class Listener : IDisposable
    {
        private readonly SolarWindHubOptions _hubOptions;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<IPEndPoint, ListenerWorker> _listeners;

        public Listener(SolarWindHubOptions hubOptions)
        {
            _listeners = new ConcurrentDictionary<IPEndPoint, ListenerWorker>();
            _hubOptions = hubOptions;
            _logger = hubOptions.LoggerFactory.CreateLogger<Listener>();
        }

        public void Listen(Uri uri, Action<HubId, Connection> onAccepted)
        {
            IPEndPoint[] endPoints = uri.ResolveLocalEndpoint();

            foreach (IPEndPoint ipEndPoint in endPoints)
            {
                var worker = new ListenerWorker(ipEndPoint, _hubOptions, onAccepted);
                if (_listeners.TryAdd(ipEndPoint, worker))
                {
                    worker.Start();
                }
                else
                {
                    worker.Dispose();
                }
            }
        }


        public void Dispose() => Parallel.ForEach(_listeners, x => x.Value.Dispose());
    }
}