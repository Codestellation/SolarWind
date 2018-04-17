using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codestellation.SolarWind.Tests
{
    public static class TestContext
    {
        //public static readonly ILoggerFactory LoggerFactory = new LoggerFactory(new ILoggerProvider[] {new ConsoleLoggerProvider((s, level) => true, false)});
        public static readonly ILoggerFactory LoggerFactory = new NullLoggerFactory();
    }
}