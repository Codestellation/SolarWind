using System;

namespace Codestellation.SolarWind
{

    public class ChannelOptions
    {
        private static readonly SolarWindCallback EmptyCallback = delegate { };
        /// <summary>
        /// Serializer which is going to be used by a <see cref="Channel"/> to process incoming and outgoing messages
        /// </summary>
        public ISerializer Serializer { get; }

        /// <summary>
        /// A callback that will be called when an incoming message is ready
        /// </summary>
        public SolarWindCallback Callback { get; }


        /// <summary>
        /// Timeout after the last incoming or outgoing message was successfully sent or received. When the specified time is gone <see cref="Codestellation.SolarWind.Channel.OnKeepAliveTimeout"/> is raised on a channel.
        /// </summary>
        public TimeSpan KeepAliveTimeout { get; set; }


        /// <summary>
        /// Creates a new instance of <see cref="ChannelOptions" /> without callback set. All incoming messages will be dropped.
        /// </summary>
        /// <param name="serializer">Am instance of <see cref="ISerializer" /></param>
        public ChannelOptions(ISerializer serializer) : this(serializer, EmptyCallback)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="ChannelOptions"/> without callback set. All incoming messages will be dropped.
        /// </summary>
        /// <param name="serializer">Am instance of <see cref="ISerializer"/></param>
        /// <param name="callback">A callback that will be called when an incoming message is ready</param>
        public ChannelOptions(ISerializer serializer, SolarWindCallback callback)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }
    }
}