using Serilog.Core;

namespace Convey.Logging;

public class LoggingService : ILoggingService
{
    internal readonly LoggingLevelSwitch LoggingLevelSwitch = new();

    public void SetLoggingLevel(string level)
        => LoggingLevelSwitch.MinimumLevel = Extensions.GetLogEventLevel(level);
}