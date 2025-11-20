using Convey.Logging.Options;
using Convey.Types;
using Elastic.Ingest.Elasticsearch;
using Elastic.Serilog.Sinks;
using Elastic.Transport;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.Grafana.Loki;
using System;
using System.Collections.Generic;

namespace Convey.Logging;

public static class Extensions
{
    private const string LoggerSectionName = "logger";
    private const string AppSectionName = "app";

    public static IHostBuilder UseLogging(
        this IHostBuilder hostBuilder,
        Action<HostBuilderContext, LoggerConfiguration> configure = null,
        string loggerSectionName = LoggerSectionName,
        string appSectionName = AppSectionName)
    {
        var loggingService = new LoggingService();

        return hostBuilder
            .ConfigureServices(services => services.AddSingleton<ILoggingService>(loggingService))
            .UseSerilog((context, loggerConfiguration) =>
            {
                if (string.IsNullOrWhiteSpace(loggerSectionName))
                {
                    loggerSectionName = LoggerSectionName;
                }

                if (string.IsNullOrWhiteSpace(appSectionName))
                {
                    appSectionName = AppSectionName;
                }

                var loggerOptions = context.Configuration.GetOptions<LoggerOptions>(loggerSectionName);
                var appOptions = context.Configuration.GetOptions<AppOptions>(appSectionName);

                MapOptions(
                    loggingService,
                    loggerOptions,
                    appOptions,
                    loggerConfiguration,
                    context.HostingEnvironment.EnvironmentName);

                configure?.Invoke(context, loggerConfiguration);
            });
    }

    private static void MapOptions(
        LoggingService loggingService,
        LoggerOptions loggerOptions,
        AppOptions appOptions,
        LoggerConfiguration loggerConfiguration,
        string environmentName)
    {
        loggingService.SetLoggingLevel(loggerOptions.Level);

        loggerConfiguration
            .Enrich.FromLogContext()
            .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch)
            .Enrich.WithProperty("Environment", environmentName)
            .Enrich.WithProperty("Application", appOptions.Service)
            .Enrich.WithProperty("Instance", appOptions.Instance)
            .Enrich.WithProperty("Version", appOptions.Version);

        loggerOptions.Tags.ForEach(
            tag =>
                loggerConfiguration.Enrich.WithProperty(tag.Key, tag.Value));

        loggerOptions.MinimumLevelOverrides.ForEach(
            @override =>
                loggerConfiguration.MinimumLevel.Override(@override.Key, GetLogEventLevel(@override.Value)));

        loggerOptions.ExcludePaths.ForEach(
            excludedPath =>
                loggerConfiguration.Filter.ByExcluding(Matching.WithProperty<string>("RequestPath", n => n.EndsWith(excludedPath))));

        loggerOptions.ExcludeProperties.ForEach(
            excludedProperty =>
                loggerConfiguration.Filter.ByExcluding(Matching.WithProperty(excludedProperty)));

        Configure(loggingService, loggerConfiguration, loggerOptions);
    }

    private static void Configure(
        LoggingService loggingService,
        LoggerConfiguration loggerConfiguration,
        LoggerOptions options)
    {
        var consoleOptions = options.Console ?? new ConsoleOptions();
        var fileOptions = options.File ?? new FileOptions();
        var elkOptions = options.Elk ?? new ElkOptions();
        var seqOptions = options.Seq ?? new SeqOptions();
        var lokiOptions = options.Loki ?? new LokiOptions();
        var azureOptions = options.Azure ?? new AzureOptions();

        if (consoleOptions.Enabled)
        {
            loggerConfiguration.WriteTo.Console();
        }

        if (fileOptions.Enabled)
        {
            var path = string.IsNullOrWhiteSpace(fileOptions.Path) ? "logs/logs.txt" : fileOptions.Path;

            if (!Enum.TryParse<RollingInterval>(fileOptions.Interval, true, out var interval))
            {
                interval = RollingInterval.Day;
            }

            loggerConfiguration
                .WriteTo.File(path, rollingInterval: interval)
                .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);
        }

        if (elkOptions.Enabled)
        {
            loggerConfiguration.WriteTo.Elasticsearch(
                [new Uri(elkOptions.Url)],
                opts =>
                {
                    opts.BootstrapMethod = BootstrapMethod.Failure;
                },
                transport =>
                {
                    if (elkOptions.BasicAuthEnabled)
                    {
                        transport.Authentication(new BasicAuthentication(elkOptions.Username, elkOptions.Password));
                    }
                    else if (elkOptions.ApiKeyAuthEnabled)
                    {
                        transport.Authentication(new ApiKey(elkOptions.ApiKey));
                    }
                })
                .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);
        }

        if (seqOptions.Enabled)
        {
            loggerConfiguration
                .WriteTo.Seq(seqOptions.Url, apiKey: seqOptions.ApiKey)
                .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);
        }

        if (lokiOptions.Enabled)
        {
            if (lokiOptions.LokiUsername is not null && lokiOptions.LokiPassword is not null)
            {
                var auth = new LokiCredentials
                {
                    Login = lokiOptions.LokiUsername,
                    Password = lokiOptions.LokiPassword
                };

                loggerConfiguration
                    .WriteTo.GrafanaLoki(
                        lokiOptions.Url,
                        credentials: auth,
                        batchPostingLimit: lokiOptions.BatchPostingLimit ?? 1000,
                        queueLimit: lokiOptions.QueueLimit,
                        period: lokiOptions.Period)
                    .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);
            }
            else
            {
                loggerConfiguration
                    .WriteTo.GrafanaLoki(
                        lokiOptions.Url,
                        batchPostingLimit: lokiOptions.BatchPostingLimit ?? 1000,
                        queueLimit: lokiOptions.QueueLimit,
                        period: lokiOptions.Period)
                    .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);
            }
        }

        if (azureOptions.Enabled)
        {
            var telemetryConfiguration = new TelemetryConfiguration
            {
                ConnectionString = azureOptions.ConnectionString
            };

            if (!string.IsNullOrEmpty(azureOptions.InstrumentationKey))
            {
                telemetryConfiguration.ConnectionString = azureOptions.InstrumentationKey;
            }

            switch (azureOptions.LogType)
            {
                case AzureLogType.Event:
                    loggerConfiguration
                        .WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Events)
                        .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);

                    break;

                case AzureLogType.Trace:
                    loggerConfiguration
                        .WriteTo.ApplicationInsights(telemetryConfiguration, TelemetryConverter.Traces)
                        .MinimumLevel.ControlledBy(loggingService.LoggingLevelSwitch);

                    break;
            }
        }
    }

    internal static LogEventLevel GetLogEventLevel(string level)
        => Enum.TryParse<LogEventLevel>(level, true, out var logLevel)
            ? logLevel
            : LogEventLevel.Information;

    public static IConveyBuilder AddCorrelationContextLogging(this IConveyBuilder builder)
    {
        builder.Services.AddTransient<CorrelationContextLoggingMiddleware>();

        return builder;
    }

    public static IApplicationBuilder UseCorrelationContextLogging(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationContextLoggingMiddleware>();

        return app;
    }

    private static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        switch (enumerable)
        {
            case null:
                return;

            case List<T> list:
                list.ForEach(action);

                return;

            case T[] array:
                Array.ForEach(array, action);

                return;
        }

        foreach (var item in enumerable)
        {
            action.Invoke(item);
        }
    }
}