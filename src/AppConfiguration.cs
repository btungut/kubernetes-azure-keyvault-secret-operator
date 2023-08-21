using Serilog.Events;

namespace Operator
{
    internal class AppConfiguration
    {
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Information;
        public bool EnableJsonLogging { get; set; } = false;

        public int WorkerCount { get; private set; } = 1;
        public TimeSpan ReconciliationFrequency { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan? ForceUpdateFrequency { get; set; } = null;
    }
}
