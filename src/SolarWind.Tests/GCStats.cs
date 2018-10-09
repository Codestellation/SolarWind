using System;

namespace Codestellation.SolarWind.Tests
{
    public readonly struct GCStats
    {
        private readonly int _gen0;
        private readonly int _gen1;
        private readonly int _gen2;

        private GCStats(int gen0, int gen1, int gen2)
        {
            _gen0 = gen0;
            _gen1 = gen1;
            _gen2 = gen2;
        }


        public static GCStats Snapshot() => new GCStats(GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));

        public GCStats Diff()
        {
            GCStats current = Snapshot();

            return new GCStats(current._gen0 - _gen0, current._gen1 - _gen1, current._gen2 - _gen2);
        }

        public override string ToString() => $"Gen0={_gen0}; Gen1={_gen1}; Gen2={_gen2}";
    }
}