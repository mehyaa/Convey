---
layout: default
title: Home
nav_order: 1
---

# Convey Documentation

Convey is a modular toolkit for building resilient, observable .NET microservices. Each package is optional and focused; compose only what you need.

> For a guided walkthrough see: [`GettingStarted.md`](./GettingStarted.md)

## Overview

Convey packages follow these principles:
- Focused responsibilities per module
- Convention over configuration (override when needed)
- Framework transparency (you retain control of ASP.NET Core pipeline)
- Production readiness (observability, security, resiliency helpers)

## Core Libraries

### Foundation
- [**Convey**](./Convey.md) - Core framework and dependency injection container
- [**Convey.WebApi**](./Convey.WebApi.md) - Web API extensions and minimal routing
- [**Convey.WebApi.CQRS**](./Convey.WebApi.CQRS.md) - CQRS integration for Web API

### Authentication & Security
- [**Convey.Auth**](./Convey.Auth.md) - JWT authentication with secret keys and certificates
- [**Convey.Auth.Distributed**](./Convey.Auth.Distributed.md) - Distributed authentication patterns
- [**Convey.Security**](./Convey.Security.md) - Security extensions, certificates, mTLS, encryption
- [**Convey.WebApi.Security**](./Convey.WebApi.Security.md) - Web API security extensions

### CQRS (Command Query Responsibility Segregation)
- [**Convey.CQRS.Commands**](./Convey.CQRS.Commands.md) - Command handling abstractions
- [**Convey.CQRS.Events**](./Convey.CQRS.Events.md) - Event handling abstractions
- [**Convey.CQRS.Queries**](./Convey.CQRS.Queries.md) - Query handling abstractions

### Message Brokers
- [**Convey.MessageBrokers**](./Convey.MessageBrokers.md) - Message broker abstractions
- [**Convey.MessageBrokers.CQRS**](./Convey.MessageBrokers.CQRS.md) - CQRS support for message brokers
- [**Convey.MessageBrokers.RabbitMQ**](./Convey.MessageBrokers.RabbitMQ.md) - RabbitMQ integration
- [**Convey.MessageBrokers.Outbox**](./Convey.MessageBrokers.Outbox.md) - Outbox pattern implementation
- [**Convey.MessageBrokers.Outbox.EntityFramework**](./Convey.MessageBrokers.Outbox.EntityFramework.md) - EF Core outbox implementation
- [**Convey.MessageBrokers.Outbox.Mongo**](./Convey.MessageBrokers.Outbox.Mongo.md) - MongoDB outbox implementation

### Persistence
- [**Convey.Persistence.MongoDB**](./Convey.Persistence.MongoDB.md) - MongoDB extensions and patterns
- [**Convey.Persistence.Redis**](./Convey.Persistence.Redis.md) - Redis extensions and caching
- [**Convey.Persistence.OpenStack.OCS**](./Convey.Persistence.OpenStack.OCS.md) - OpenStack OCS support

### Service Discovery & Load Balancing
- [**Convey.Discovery.Consul**](./Convey.Discovery.Consul.md) - Consul service registry integration
- [**Convey.LoadBalancing.Fabio**](./Convey.LoadBalancing.Fabio.md) - Fabio load balancer integration

### HTTP & API
- [**Convey.HTTP**](./Convey.HTTP.md) - HTTP client extensions
- [**Convey.HTTP.RestEase**](./Convey.HTTP.RestEase.md) - RestEase integration for typed HTTP clients
- [**Convey.Docs.Swagger**](./Convey.Docs.Swagger.md) - Swagger/OpenAPI documentation
- [**Convey.WebApi.Swagger**](./Convey.WebApi.Swagger.md) - Swagger integration for Web API

### Observability
- [**Convey.Logging**](./Convey.Logging.md) - Logging extensions with Serilog integration
- [**Convey.Logging.CQRS**](./Convey.Logging.CQRS.md) - CQRS logging integration
- [**Convey.Metrics.AppMetrics**](./Convey.Metrics.AppMetrics.md) - AppMetrics integration
- [**Convey.Metrics.Prometheus**](./Convey.Metrics.Prometheus.md) - Prometheus metrics integration
- [**Convey.Tracing.Jaeger**](./Convey.Tracing.Jaeger.md) - Jaeger distributed tracing
- [**Convey.Tracing.Jaeger.RabbitMQ**](./Convey.Tracing.Jaeger.RabbitMQ.md) - Jaeger tracing for RabbitMQ

### Configuration & Secrets
- [**Convey.Secrets.Vault**](./Convey.Secrets.Vault.md) - HashiCorp Vault integration for secrets management

## Getting Started (TL;DR)
```pwsh
dotnet add package Convey
dotnet add package Convey.WebApi
dotnet add package Convey.CQRS.Commands
dotnet add package Convey.MessageBrokers.RabbitMQ
```

```csharp
builder.Services.AddConvey()
    .AddWebApi()
    .AddCommandHandlers()
    .AddInMemoryCommandDispatcher() // or use RabbitMQ
    .AddRabbitMq();
```

See [`GettingStarted.md`](./GettingStarted.md) for a full walkthrough including CQRS, MongoDB & RabbitMQ setup.

## Sample Applications

Explore the `samples/` directory for full services demonstrating composition patterns:
- `Conveyor.Services.Orders` – Order management
- `Conveyor.Services.Deliveries` – Delivery tracking
- `Conveyor.Services.Pricing` – Pricing calculations

## Contributing

Maintained by [devmentors.io](http://devmentors.io). Contributions are welcome—open an issue first for larger changes. Please keep docs concise & consistent with the style guide.

## License

See the root `LICENSE` file.

---
Need terminology or formatting guidance? See [`STYLEGUIDE.md`](./STYLEGUIDE.md).
