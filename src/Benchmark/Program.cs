using System.Reflection;
using BenchmarkDotNet.Running;
using Oakton;

namespace Benchmark
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            var executor = CommandExecutor.For(_ =>
            {
                // Find and apply all command classes discovered
                // in this assembly
                _.RegisterCommands(typeof(Program).GetTypeInfo().Assembly);
            });

            return executor.Execute(args);
        }
    }
}