namespace Convey.Logging.Options;

public class AzureOptions
{
    public bool Enabled { get; set; }
    public AzureLogType LogType { get; set; }
    public string ConnectionString { get; set; }
    public string InstrumentationKey { get; set; }
}

public enum AzureLogType
{
    Event,
    Trace
}