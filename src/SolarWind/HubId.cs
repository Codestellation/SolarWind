using System;

namespace Codestellation.SolarWind
{
    public struct HubId : IEquatable<HubId>
    {
        public string Id { get; }

        public HubId(string id)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Must be not empty string", nameof(id))
                : id;
        }

        public bool Equals(HubId other) => string.Equals(Id, other.Id);

        public override bool Equals(object obj) => obj is HubId id && Equals(id);

        public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

        public static bool operator ==(HubId left, HubId right) => left.Equals(right);

        public static bool operator !=(HubId left, HubId right) => !left.Equals(right);

        public override string ToString() => Id;
    }
}