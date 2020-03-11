using System;
using Oakton;

namespace Benchmark.Commands
{
    public class ProfileCommand : OaktonCommand<ProfileInput>
    {
        public override bool Execute(ProfileInput input)
        {
            var benchmark = new PingPongBenchmark();
            try
            {
                Console.WriteLine("Starting setup");
                benchmark.GlobalSetup();
                benchmark.IterationSetup();

                Console.WriteLine("Starting profiling");
                benchmark.Run_ping_pong_benchmark();

                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
            }
            finally
            {
                benchmark.GlobalCleanup();
            }

            return true;
        }
    }
}