using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Codestellation.SolarWind.Internals;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codestellation.SolarWind
{
    public class SolarWindHubOptions
    {
        private TimeSpan _sendTimeout;
        private TimeSpan _receiveTimeout;
        private readonly CancellationTokenSource _cancellationSource;

        public X509Certificate Certificate { get; set; }

        public bool NoDelay { get; set; }

        public TimeSpan SendTimeout
        {
            get => _sendTimeout;
            set => _sendTimeout = ValidateTimeout(value);
        }

        public TimeSpan ReceiveTimeout
        {
            get => _receiveTimeout;
            set => _receiveTimeout = ValidateTimeout(value);
        }

        internal CancellationToken Cancellation => _cancellationSource.Token;


        /// <summary>
        /// Used to identify application level connections and preserve session between reconnections.
        /// </summary>
        public HubId HubId { get; }

        public SolarWindHubOptions(HubId hubId, ILoggerFactory loggerFactory)
        {
            if (hubId == default)
            {
                throw new ArgumentException("Must not be the default value", nameof(hubId));
            }

            HubId = hubId;
            loggerFactory ??= NullLoggerFactory.Instance;

            NoDelay = true;
            SendTimeout = TimeSpan.FromSeconds(1);
            ReceiveTimeout = TimeSpan.FromSeconds(1);

            _cancellationSource = new CancellationTokenSource();

            ConnectionLogger = loggerFactory.CreateLogger<Connection>();
            ChannelLogger = loggerFactory.CreateLogger<Channel>();
            ListenerWorkerLogger = loggerFactory.CreateLogger<ListenerWorker>();
            SessionLogger = loggerFactory.CreateLogger<Session>();
        }

        internal ILogger<Session> SessionLogger { get; set; }

        internal ILogger<ListenerWorker> ListenerWorkerLogger { get; }

        internal ILogger<Channel> ChannelLogger { get; }

        internal ILogger<Connection> ConnectionLogger { get; }

        public SolarWindHubOptions(ILoggerFactory loggerFactory)
            : this(HubId.Generate(), loggerFactory)
        {
        }

        internal SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();

        private TimeSpan ValidateTimeout(TimeSpan value)
        {
            if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
            {
                var message = $"Must be greater than or equal to {nameof(TimeSpan)}.{nameof(TimeSpan.Zero)} equal to {nameof(Timeout)}.{nameof(Timeout.InfiniteTimeSpan)}";
                throw new ArgumentOutOfRangeException(nameof(value), message);
            }

            return value;
        }

        public void RaiseCancellation() => _cancellationSource.Cancel();
    }
}