using System;

namespace Codestellation.SolarWind
{
    public struct ChannelId : IEquatable<ChannelId>
    {
        public HubId Local { get; }
        public HubId Remote { get; }

        public ChannelId(HubId local, HubId remote)
        {
            Local = local;
            Remote = remote;
        }

        public bool Equals(ChannelId other) => Local.Equals(other.Local) && Remote.Equals(other.Remote);

        public override bool Equals(object obj) => obj is ChannelId id && Equals(id);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Local.GetHashCode() * 397) ^ Remote.GetHashCode();
            }
        }

        public static bool operator ==(ChannelId left, ChannelId right) => left.Equals(right);

        public static bool operator !=(ChannelId left, ChannelId right) => !left.Equals(right);
    }
}