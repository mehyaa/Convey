using System.Collections.Generic;

namespace Convey.Tracing.Jaeger;

public class JaegerOptions
{
    public bool Enabled { get; set; }
    public string Endpoint { get; set; }
    public string Protocol { get; set; }
    public IList<string> ExcludePaths { get; set; }

}