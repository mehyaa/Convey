using Convey.Tracing.Jaeger.Builders;
using Convey.Tracing.Jaeger.Tracers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.ResourceDetectors.Container;
using OpenTelemetry.Shims.OpenTracing;
using OpenTelemetry.Trace;
using OpenTracing;
using OpenTracing.Util;
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
            var defaultTracer = ConveyDefaultTracer.Create();
            builder.Services.AddSingleton(defaultTracer);
            return builder;
        }

        if (!builder.TryRegister(RegistryName))
        {
            return builder;
        }

        var telemetryBuilder =
            builder.Services
                .AddOpenTelemetry()
                .WithTracing(builder =>
                    builder
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
                            "http" or "protobuf" or "httpprotobuf" or "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                            "grpc" => OtlpExportProtocol.Grpc,
                            _ => OtlpExportProtocol.Grpc
                        };

                        configure.Endpoint = new(options.Endpoint ?? (configure.Protocol == OtlpExportProtocol.Grpc ? "localhost:4317" : "localhost:4318"));
                    })
                );

        builder.Services.AddSingleton<ITracer>(serviceProvider =>
        {
            var traceProvider = serviceProvider.GetRequiredService<TracerProvider>();
            var tracer = new TracerShim(traceProvider, Propagators.DefaultTextMapPropagator);
            GlobalTracer.RegisterIfAbsent(tracer);
            return tracer;
        });

        return builder;
    }

    public static IApplicationBuilder UseJaeger(this IApplicationBuilder app)
    {
        // Could be extended with some additional middleware
        using var scope = app.ApplicationServices.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<JaegerOptions>();
        return app;
    }
}