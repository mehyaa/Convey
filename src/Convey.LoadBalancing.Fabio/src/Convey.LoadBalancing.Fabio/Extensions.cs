using Convey.Discovery.Consul;
using Convey.Discovery.Consul.Builders;
using Convey.Discovery.Consul.Models;
using Convey.HTTP;
using Convey.LoadBalancing.Fabio.Builders;
using Convey.LoadBalancing.Fabio.Http;
using Convey.LoadBalancing.Fabio.MessageHandlers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Convey.LoadBalancing.Fabio;

public static class Extensions
{
    private const string RegistryName = "loadBalancing.fabio";
    private const string SectionName = "fabio";

    private const int DefaultTimeoutSeconds = 120;

    public static IConveyBuilder AddFabio(
        this IConveyBuilder builder,
        string sectionName = SectionName,
        string consulSectionName = "consul",
        string httpClientSectionName = "httpClient")
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            sectionName = SectionName;
        }

        var fabioOptions = builder.GetOptions<FabioOptions>(sectionName);
        var consulOptions = builder.GetOptions<ConsulOptions>(consulSectionName);
        var httpClientOptions = builder.GetOptions<HttpClientOptions>(httpClientSectionName);

        return builder.AddFabio(
            consulOptions,
            fabioOptions,
            httpClientOptions,
            b => b.AddConsul(consulOptions, httpClientOptions));
    }

    public static IConveyBuilder AddFabio(
        this IConveyBuilder builder,
        Func<IFabioOptionsBuilder, IFabioOptionsBuilder> buildOptions,
        Func<IConsulOptionsBuilder, IConsulOptionsBuilder> buildConsulOptions,
        HttpClientOptions httpClientOptions)
    {
        var consulOptions = buildConsulOptions.Invoke(new ConsulOptionsBuilder()).Build();
        var fabioOptions = buildOptions(new FabioOptionsBuilder()).Build();

        return builder.AddFabio(
            consulOptions,
            fabioOptions,
            httpClientOptions,
            b => b.AddConsul(buildConsulOptions, httpClientOptions));
    }

    public static IConveyBuilder AddFabio(
        this IConveyBuilder builder,
        FabioOptions fabioOptions,
        ConsulOptions consulOptions,
        HttpClientOptions httpClientOptions)
        => builder.AddFabio(
            consulOptions,
            fabioOptions,
            httpClientOptions,
            b => b.AddConsul(consulOptions, httpClientOptions));

    private static IConveyBuilder AddFabio(
        this IConveyBuilder builder,
        ConsulOptions consulOptions,
        FabioOptions fabioOptions,
        HttpClientOptions httpClientOptions,
        Action<IConveyBuilder> registerConsul)
    {
        registerConsul.Invoke(builder);

        builder.Services.AddSingleton(fabioOptions);

        if (!fabioOptions.Enabled || !builder.TryRegister(RegistryName))
        {
            return builder;
        }

        if (httpClientOptions.Type?.ToLowerInvariant() == "fabio")
        {
            builder.Services.AddTransient<FabioMessageHandler>();

            builder.Services.AddHttpClient<IFabioHttpClient, FabioHttpClient>("fabio-http", configure =>
                {
                    configure.Timeout = httpClientOptions.Timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds);
                })
                .AddHttpMessageHandler<FabioMessageHandler>();

            builder.RemoveHttpClient();

            builder.Services.AddHttpClient<IHttpClient, FabioHttpClient>("fabio", configure =>
                {
                    configure.Timeout = httpClientOptions.Timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds);
                })
                .AddHttpMessageHandler<FabioMessageHandler>();
        }

        var registration =
            builder.Services.GetConsulRegistration() ??
            throw new OperationCanceledException("No Consul service registration found");

        var tags = GetFabioTags(consulOptions.Service, fabioOptions.Service);

        if (registration.Tags is null)
        {
            registration.Tags = tags;
        }
        else
        {
            var allTags = registration.Tags.ToList();

            allTags.AddRange(tags);

            registration.Tags = allTags;
        }

        return builder;
    }

    public static void AddFabioHttpClient(this IConveyBuilder builder, string clientName, string serviceName)
        => builder.Services.AddHttpClient<IHttpClient, FabioHttpClient>(clientName, (sp, configure) =>
            {
                var httpClientOptions = sp.GetRequiredService<HttpClientOptions>();

                configure.Timeout = httpClientOptions.Timeout ?? TimeSpan.FromSeconds(DefaultTimeoutSeconds);
            })
            .AddHttpMessageHandler(c => new FabioMessageHandler(c.GetRequiredService<FabioOptions>(), serviceName));

    private static ServiceRegistration GetConsulRegistration(this IServiceCollection services)
    {
        var serviceDescriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(ServiceRegistration));

        return serviceDescriptor?.ImplementationInstance as ServiceRegistration;
    }

    private static IList<string> GetFabioTags(string consulService, string fabioService)
    {
        var service = (string.IsNullOrWhiteSpace(fabioService) ? consulService : fabioService).ToLowerInvariant();

        return [$"urlprefix-/{service} strip=/{service}"];
    }
}