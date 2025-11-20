# Convey

> A lightweight, modular toolkit for rapidly building production-ready .NET (Core) microservices.

## Documentation & Demo
Read the full docs at **[convey-stack.github.io](https://convey-stack.github.io)** or watch a short demo **[on YouTube](https://www.youtube.com/watch?v=cxEXx4UT1FI)**.

## Key Features
Convey provides building blocks so you can focus on your domain instead of wiring infrastructure:

- **Authentication**: [JWT](http://jwt.io) with secret key & certificate support (+ mTLS helpers)
- **CQRS**: Simple abstractions for commands, queries & events
- **Service Discovery**: [Consul](https://www.consul.io) integration
- **API Documentation**: [Swagger / OpenAPI](https://swagger.io) extensions
- **HTTP Clients**: [RestEase](https://github.com/canton7/RestEase) integration
- **Load Balancing**: [Fabio](https://github.com/fabiolb/fabio) support
- **Logging**: Extensions for [Serilog](https://serilog.net/) + sinks (Seq / ELK / Loki)
- **Messaging**: Broker abstractions, CQRS over messages & [RabbitMQ](https://www.rabbitmq.com) integration
- **Reliability**: Inbox + Outbox pattern (EF Core / MongoDB implementations)
- **Metrics**: [AppMetrics](https://www.app-metrics.io) & [Prometheus](https://prometheus.io) integration
- **Persistence**: Extensions for [MongoDB](https://www.mongodb.com/cloud), [Redis](https://redis.io), OpenStack OCS
- **Secrets**: [Vault](https://www.vaultproject.io) (KV, dynamic credentials, PKI, etc.)
- **Security**: Certificates, mTLS helpers, encryption utilities
- **Tracing**: [Jaeger](https://www.jaegertracing.io) (incl. RabbitMQ propagation)
- **Web API**: Minimal routing-based API, CQRS endpoint helpers

## Quick Start
```pwsh
dotnet new webapi -n SampleService
cd SampleService
dotnet add package Convey
dotnet add package Convey.WebApi
dotnet add package Convey.CQRS.Commands
dotnet add package Convey.MessageBrokers.RabbitMQ
```

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
	.AddWebApi()
	.AddCommandHandlers()
	.AddInMemoryCommandDispatcher()
	.AddRabbitMq();

var app = builder.Build();
app.UseRouting();
app.UseEndpoints(endpoints => endpoints.Get("/", ctx => ctx.Response.WriteAsync("OK")));
app.Run();
```

More examples are available under the `samples/` directory and the docs site Getting Started guide.

## Contributing
Issues & PRs are welcome. Please follow conventional commit style and keep changes focused.

## License
See the `LICENSE` file for details.

Created & maintained by [devmentors.io](http://devmentors.io).