using Convey.Tracing.Jaeger.Builders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.ResourceDetectors.Container;
using OpenTelemetry.Trace;
using System;
using System.Threading;

namespace Convey.Tracing.Jaeger;

public static class Extensions
{
    private const string SectionName = "jaeger";
    private const string RegistryName = "tracing.jaeger";

    private static int _initialized;

    public static IConveyBuilder AddJaeger(this IConveyBuilder builder, string sectionName = SectionName)
    {
        var options = builder.GetOptions<JaegerOptions>(string.IsNullOrWhiteSpace(sectionName) ? SectionName : sectionName);
        return builder.AddJaeger(options);
    }

    public static IConveyBuilder AddJaeger(this IConveyBuilder builder, Func<IJaegerOptionsBuilder, IJaegerOptionsBuilder> buildOptions)
    {
        var options = buildOptions(new JaegerOptionsBuilder()).Build();
        return builder.AddJaeger(options);
    }

    public static IConveyBuilder AddJaeger(this IConveyBuilder builder, JaegerOptions options)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return builder;
        }

        builder.Services.AddSingleton(options);

        if (!options.Enabled)
        {
            return builder;
        }

        if (!builder.TryRegister(RegistryName))
        {
            return builder;
        }

        builder.Services
            .AddOpenTelemetry()
            .WithTracing(providerBuilder =>
                providerBuilder
                    .ConfigureResource(resource => resource.AddDetector(new ContainerResourceDetector()))
                    .AddAspNetCoreInstrumentation(string.Empty, aspCoreOptions =>
                    {
                        aspCoreOptions.Filter =
                            context =>
                                options.ExcludePaths?.Contains(context.Request.Path.ToString()) != true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter("tracing", configure =>
                    {
                        configure.Protocol = options.Protocol.ToLowerInvariant() switch
                        {
                            "http" or "protobuf" or "httpprotobuf" or "http/protobuf" =>
                                OtlpExportProtocol.HttpProtobuf,
                            "grpc" => OtlpExportProtocol.Grpc,
                            _ => OtlpExportProtocol.Grpc
                        };

                        configure.Endpoint = new(options.Endpoint ?? (configure.Protocol == OtlpExportProtocol.Grpc
                            ? "localhost:4317"
                            : "localhost:4318"));
                    })
            );

        return builder;
    }

    public static IApplicationBuilder UseJaeger(this IApplicationBuilder app)
    {
        // Could be extended with some additional middleware
        using var scope = app.ApplicationServices.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<JaegerOptions>();
        return app;
    }
}