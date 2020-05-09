using Oakton;

namespace Benchmark.Commands.Compare
{
    public class CompareInput
    {
        [Description("Path to BenchmarkDotNet output folder with csv files to be considered as baseline")]
        public string Baseline { get; set; }

        [Description("Path to BenchmarkDotNet output folder with csv files to be compare with the baseline result")]
        public string Current { get; set; }
    }
}