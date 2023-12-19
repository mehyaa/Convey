using System.Collections.Generic;

namespace Convey.Tracing.Jaeger;

public interface IJaegerOptionsBuilder
{
    IJaegerOptionsBuilder Enable(bool enabled);
    IJaegerOptionsBuilder WithEndpoint(string endpoint);
    IJaegerOptionsBuilder WithProtocol(string protocol);
    IJaegerOptionsBuilder WithExcludePaths(IList<string> excludePaths);
    JaegerOptions Build();
}