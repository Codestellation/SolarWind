using System;
using System.Globalization;

namespace Codestellation.SolarWind
{
    public readonly struct MessageTypeId : IEquatable<MessageTypeId>
    {
        //Should contain HubId. (later it may contain auth information)
        internal static readonly MessageTypeId Handshake = new MessageTypeId(-1L);

        //Acknowledges that a group of messages are successfully received.  
        internal static readonly MessageTypeId Ack = new MessageTypeId(-10L);

        //Server or client should send it in case of graceful shutdown. 
        internal static readonly MessageTypeId Bye = new MessageTypeId(-100L);

        private readonly int _id;

        public MessageTypeId(int id)
        {
            if (id < 0)
            {
                throw new ArgumentException("Must be equal to or greater than 0", nameof(id));
            }

            _id = id;
        }

        //It's a life hack to have to constructors with the same signature
        private MessageTypeId(long id) => _id = (int)id;


        /// <inheritdoc />
        public bool Equals(MessageTypeId other) => _id == other._id;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is MessageTypeId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => _id;

        public static bool operator ==(MessageTypeId left, MessageTypeId right) => left.Equals(right);

        public static bool operator !=(MessageTypeId left, MessageTypeId right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => _id.ToString(CultureInfo.InvariantCulture);
    }
}