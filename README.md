# Convey

> A lightweight, modular toolkit for rapidly building production-ready .NET (Core) microservices.

## Documentation & Demo
Read the full docs at **[mehyaa.github.io/Convey](https://mehyaa.github.io/Convey)** or watch a short demo **[on YouTube](https://www.youtube.com/watch?v=cxEXx4UT1FI)**.

You can also find detailed documentation in the [docs/](docs/) directory.

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

## Core Libraries

### Foundation
- [**Convey**](docs/Convey.md) - Core framework and dependency injection container
- [**Convey.WebApi**](docs/Convey.WebApi.md) - Web API extensions and minimal routing
- [**Convey.WebApi.CQRS**](docs/Convey.WebApi.CQRS.md) - CQRS integration for Web API

### Authentication & Security
- [**Convey.Auth**](docs/Convey.Auth.md) - JWT authentication with secret keys and certificates
- [**Convey.Auth.Distributed**](docs/Convey.Auth.Distributed.md) - Distributed authentication patterns
- [**Convey.Security**](docs/Convey.Security.md) - Security extensions, certificates, mTLS, encryption
- [**Convey.WebApi.Security**](docs/Convey.WebApi.Security.md) - Web API security extensions

### CQRS (Command Query Responsibility Segregation)
- [**Convey.CQRS.Commands**](docs/Convey.CQRS.Commands.md) - Command handling abstractions
- [**Convey.CQRS.Events**](docs/Convey.CQRS.Events.md) - Event handling abstractions
- [**Convey.CQRS.Queries**](docs/Convey.CQRS.Queries.md) - Query handling abstractions

### Message Brokers
- [**Convey.MessageBrokers**](docs/Convey.MessageBrokers.md) - Message broker abstractions
- [**Convey.MessageBrokers.CQRS**](docs/Convey.MessageBrokers.CQRS.md) - CQRS support for message brokers
- [**Convey.MessageBrokers.RabbitMQ**](docs/Convey.MessageBrokers.RabbitMQ.md) - RabbitMQ integration
- [**Convey.MessageBrokers.Outbox**](docs/Convey.MessageBrokers.Outbox.md) - Outbox pattern implementation
- [**Convey.MessageBrokers.Outbox.EntityFramework**](docs/Convey.MessageBrokers.Outbox.EntityFramework.md) - EF Core outbox implementation
- [**Convey.MessageBrokers.Outbox.Mongo**](docs/Convey.MessageBrokers.Outbox.Mongo.md) - MongoDB outbox implementation

### Persistence
- [**Convey.Persistence.MongoDB**](docs/Convey.Persistence.MongoDB.md) - MongoDB extensions and patterns
- [**Convey.Persistence.Redis**](docs/Convey.Persistence.Redis.md) - Redis extensions and caching
- [**Convey.Persistence.OpenStack.OCS**](docs/Convey.Persistence.OpenStack.OCS.md) - OpenStack OCS support

### Service Discovery & Load Balancing
- [**Convey.Discovery.Consul**](docs/Convey.Discovery.Consul.md) - Consul service registry integration
- [**Convey.LoadBalancing.Fabio**](docs/Convey.LoadBalancing.Fabio.md) - Fabio load balancer integration

### HTTP & API
- [**Convey.HTTP**](docs/Convey.HTTP.md) - HTTP client extensions
- [**Convey.HTTP.RestEase**](docs/Convey.HTTP.RestEase.md) - RestEase integration for typed HTTP clients
- [**Convey.Docs.Swagger**](docs/Convey.Docs.Swagger.md) - Swagger/OpenAPI documentation
- [**Convey.WebApi.Swagger**](docs/Convey.WebApi.Swagger.md) - Swagger integration for Web API

### Observability
- [**Convey.Logging**](docs/Convey.Logging.md) - Logging extensions with Serilog integration
- [**Convey.Logging.CQRS**](docs/Convey.Logging.CQRS.md) - CQRS logging integration
- [**Convey.Metrics.AppMetrics**](docs/Convey.Metrics.AppMetrics.md) - AppMetrics integration
- [**Convey.Metrics.Prometheus**](docs/Convey.Metrics.Prometheus.md) - Prometheus metrics integration
- [**Convey.Tracing.Jaeger**](docs/Convey.Tracing.Jaeger.md) - Jaeger distributed tracing
- [**Convey.Tracing.Jaeger.RabbitMQ**](docs/Convey.Tracing.Jaeger.RabbitMQ.md) - Jaeger tracing for RabbitMQ

### Configuration & Secrets
- [**Convey.Secrets.Vault**](docs/Convey.Secrets.Vault.md) - HashiCorp Vault integration for secrets management

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

More examples are available under the `samples/` directory and the docs site [Getting Started guide](docs/GettingStarted.md).

## Sample Applications

Explore the `samples/` directory for full services demonstrating composition patterns:
- `Conveyor.Services.Orders` – Order management
- `Conveyor.Services.Deliveries` – Delivery tracking
- `Conveyor.Services.Pricing` – Pricing calculations

## Contributing
Issues & PRs are welcome. Please follow conventional commit style and keep changes focused.

## License
See the `LICENSE` file for details.

Created by [devmentors.io](http://devmentors.io)
Is being maintained by [mehyaa](https://github.com/mehyaa)