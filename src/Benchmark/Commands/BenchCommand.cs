using BenchmarkDotNet.Running;
using Oakton;

namespace Benchmark.Commands
{
    public class BenchCommand : OaktonCommand<BenchInput>
    {
        public override bool Execute(BenchInput input)
        {
            BenchmarkRunner.Run<PingPongBenchmark>();
            return true;
        }
    }
}