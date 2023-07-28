using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildMonitor.Logging
{
    public static class LoggerFactory
    {
        public static ILoggerFactory Global { get; set; } = new NullLoggerFactory();
    }
}

