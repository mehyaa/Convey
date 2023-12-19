using Convey;
using Convey.Discovery.Consul;
using Convey.LoadBalancing.Fabio;
using Convey.Logging;
using Convey.Metrics.Prometheus;
using Convey.Tracing.Jaeger;
using Convey.WebApi;
using Convey.WebApi.Security;
using Conveyor.Services.Pricing;
using Conveyor.Services.Pricing.DTO;
using Conveyor.Services.Pricing.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Open.Serialization.Json;

await CreateHostBuilder(args).Build().RunAsync();

static IHostBuilder CreateHostBuilder(string[] args)
    => Host
        .CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
            webBuilder
                .ConfigureServices(services =>
                    services
                        .AddConvey()
                        .AddErrorHandler<ExceptionToResponseMapper>()
                        .AddServices()
                        .AddConsul()
                        .AddFabio()
                        .AddJaeger()
                        .AddPrometheus()
                        .AddWebApi())
                .Configure(app =>
                    app
                        .UseErrorHandler()
                        .UsePrometheus()
                        .UseJaeger()
                        .UseRouting()
                        .UseCertificateAuthentication()
                        .UseAuthentication()
                        .UseAuthorization()
                        .UseEndpoints(endpoints => endpoints
                            .Get("", ctx => ctx.Response.WriteAsync("Pricing Service"))
                            .Get("ping", ctx => ctx.Response.WriteAsync("pong"))
                            .Get<GetOrderPricing>("orders/{orderId}/pricing", async (query, ctx) =>
                                await ctx.RequestServices.GetRequiredService<IJsonSerializer>()
                                    .SerializeAsync(ctx.Response.Body, new PricingDto
                                    {
                                        OrderId = query.OrderId,
                                        TotalAmount = 20.50m
                                    }))));
        })
        .UseLogging();