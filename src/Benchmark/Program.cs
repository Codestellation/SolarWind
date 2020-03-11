using BenchmarkDotNet.Running;

namespace Benchmark
{
    internal class Program
    {
        private static void Main() => BenchmarkRunner.Run<PingPongBenchmark>();
    }
}