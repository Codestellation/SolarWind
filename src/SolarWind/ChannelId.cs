using System;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Uniquely identifies channel from a local to remote hub
    /// </summary>
    public struct ChannelId : IEquatable<ChannelId>
    {
        /// <summary>
        /// Local hub identifier
        /// </summary>
        public HubId Local { get; }

        /// <summary>
        /// Remote hub identifier
        /// </summary>
        public HubId Remote { get; }

        /// <summary>
        /// Initializes a new instance of <see cref="ChannelId" /> structure
        /// </summary>
        /// <param name="local">Local hub identifier</param>
        /// <param name="remote">Remote hub identifier</param>
        public ChannelId(HubId local, HubId remote)
        {
            Local = local;
            Remote = remote;
        }

        /// <inheritdoc />
        public bool Equals(ChannelId other) => Local.Equals(other.Local) && Remote.Equals(other.Remote);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is ChannelId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (Local.GetHashCode() * 397) ^ Remote.GetHashCode();
            }
        }

        /// <summary>
        /// Tests two instances of <see cref="ChannelId" /> for equality
        /// </summary>
        /// <returns>True if left and right operands are equal</returns>
        public static bool operator ==(ChannelId left, ChannelId right) => left.Equals(right);

        /// <summary>
        /// Tests two instances of <see cref="ChannelId" /> for equality
        /// </summary>
        /// <returns>True if left and right operands are not equal</returns>
        public static bool operator !=(ChannelId left, ChannelId right) => !left.Equals(right);
    }
}