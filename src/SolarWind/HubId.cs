using System;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Identifies a <see cref="SolarWindHub" /> instance.
    /// </summary>
    public readonly struct HubId : IEquatable<HubId>
    {
        public static readonly HubId Empty = default;
        public string Id { get; }

        public HubId(string id)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Must be not empty string", nameof(id))
                : id;
        }

        /// <inheritdoc />
        public bool Equals(HubId other) => string.Equals(Id, other.Id, StringComparison.Ordinal);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is HubId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;

        public static bool operator ==(HubId left, HubId right) => left.Equals(right);

        public static bool operator !=(HubId left, HubId right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => Id;
    }
}