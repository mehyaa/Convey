using System.Collections.Generic;

namespace Convey.Tracing.Jaeger.Builders;

internal sealed class JaegerOptionsBuilder : IJaegerOptionsBuilder
{
    private readonly JaegerOptions _options = new();

    public IJaegerOptionsBuilder Enable(bool enabled)
    {
        _options.Enabled = enabled;
        return this;
    }

    public IJaegerOptionsBuilder WithEndpoint(string endpoint)
    {
        _options.Endpoint = endpoint;
        return this;
    }

    public IJaegerOptionsBuilder WithProtocol(string protocol)
    {
        _options.Protocol = protocol;
        return this;
    }

    public IJaegerOptionsBuilder WithExcludePaths(IList<string> excludePaths)
    {
        _options.ExcludePaths = excludePaths;
        return this;
    }

    public JaegerOptions Build()
        => _options;
}