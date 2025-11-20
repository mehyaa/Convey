using Convey.Types;
using Figgle.Fonts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Convey;

public static class Extensions
{
    private const string SectionName = "app";

    public static IConveyBuilder AddConvey(
        this IServiceCollection services,
        string sectionName = SectionName,
        IConfiguration configuration = null)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            sectionName = SectionName;
        }

        var builder = ConveyBuilder.Create(services, configuration);

        var options = builder.GetOptions<AppOptions>(sectionName);

        services.AddMemoryCache();

        services.AddSingleton(options);
        services.AddSingleton<IServiceId, ServiceId>();

        services.AddHostedService<StartupInitializer>();

        if (!options.DisplayBanner || string.IsNullOrWhiteSpace(options.Name))
        {
            return builder;
        }

        var version = options.DisplayVersion ? $" {options.Version}" : string.Empty;

        Console.WriteLine(FiggleFonts.Doom.Render($"{options.Name}{version}"));

        return builder;
    }

    public static IConveyBuilder AddInitializer<TInitializer>(this IConveyBuilder builder)
        where TInitializer : class, IInitializer
    {
        builder.Services.AddTransient<IInitializer, TInitializer>();

        return builder;
    }

    public static IConveyBuilder AddInitializer<TInitializer>(
        this IConveyBuilder builder,
        Func<IServiceProvider, TInitializer> resolver)
        where TInitializer : class, IInitializer
    {
        builder.Services.AddTransient<IInitializer>(resolver);

        return builder;
    }

    public static TModel GetOptions<TModel>(this IConfiguration configuration, string sectionName)
        where TModel : new()
    {
        var model = new TModel();
        configuration.GetSection(sectionName).Bind(model);
        return model;
    }

    public static TModel GetOptions<TModel>(this IConveyBuilder builder, string settingsSectionName)
        where TModel : new()
    {
        if (builder.Configuration is not null)
        {
            return builder.Configuration.GetOptions<TModel>(settingsSectionName);
        }

        using var serviceProvider = builder.Services.BuildServiceProvider();

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        return configuration.GetOptions<TModel>(settingsSectionName);
    }

    public static string Underscore(this string value)
        => string.Concat(value.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x : x.ToString()))
            .ToLowerInvariant();
}