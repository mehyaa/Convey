# Convey.Tracing.Jaeger.RabbitMQ

Advanced distributed tracing integration combining Jaeger tracing with RabbitMQ message broker monitoring, providing end-to-end observability across microservices communication patterns.

## Installation

```bash
dotnet add package Convey.Tracing.Jaeger.RabbitMQ
```

## Overview

Convey.Tracing.Jaeger.RabbitMQ provides:
- **Message tracing** - Complete tracing of RabbitMQ message flows
- **Cross-service correlation** - End-to-end trace correlation across services
- **Consumer/producer tracking** - Detailed performance monitoring of message operations
- **Queue metrics** - Queue depth, processing times, and error rates
- **Span enrichment** - Rich metadata for message routing and processing
- **Error tracking** - Comprehensive error reporting and trace correlation
- **Performance insights** - Message throughput and latency analysis
- **Service mesh integration** - Integration with service mesh observability

## Configuration

### Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJaeger()
    .AddRabbitMq()
    .AddJaegerRabbitMqTracing();

var app = builder.Build();
app.Run();
```

### Advanced Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddConvey()
    .AddJaeger(jaeger =>
    {
        jaeger.Enabled = true;
        jaeger.ServiceName = "user-service";
        jaeger.UdpHost = "jaeger-agent";
        jaeger.UdpPort = 6831;
        jaeger.MaxPacketSize = 65000;
        jaeger.Sampler = "const";
        jaeger.MaxTracesPerSecond = 100;
        jaeger.ExcludePaths = new[] { "/health", "/metrics" };
    })
    .AddRabbitMq(rabbit =>
    {
        rabbit.ConnectionName = "user-service";
        rabbit.HostNames = new[] { "rabbitmq" };
        rabbit.Port = 5672;
        rabbit.VirtualHost = "/";
        rabbit.Username = "user";
        rabbit.Password = "password";
        rabbit.Exchange = new ExchangeOptions
        {
            Name = "user_events",
            Type = "topic",
            Durable = true
        };
    })
    .AddJaegerRabbitMqTracing(tracing =>
    {
        tracing.TraceProducers = true;
        tracing.TraceConsumers = true;
        tracing.TraceMessageProcessing = true;
        tracing.IncludeMessagePayload = false;
        tracing.MaxPayloadLength = 1000;
        tracing.SensitiveHeaders = new[] { "authorization", "x-api-key" };
        tracing.IgnoreQueues = new[] { "health-check", "metrics" };
        tracing.TagMessage = true;
        tracing.TagRoutingKey = true;
        tracing.TagExchange = true;
        tracing.TagConsumerInfo = true;
    });

var app = builder.Build();
app.Run();
```

### Service-Specific Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);

// Producer service configuration
builder.Services.AddConvey()
    .AddJaeger(jaeger =>
    {
        jaeger.ServiceName = "order-service";
        jaeger.Enabled = true;
    })
    .AddRabbitMq()
    .AddJaegerRabbitMqTracing(tracing =>
    {
        tracing.TraceProducers = true;
        tracing.TraceConsumers = false; // This service only produces
        tracing.TraceMessageProcessing = false;
        tracing.IncludeMessagePayload = true;
        tracing.TagMessage = true;
        tracing.TagRoutingKey = true;
    });

// Consumer service configuration
builder.Services.AddConvey()
    .AddJaeger(jaeger =>
    {
        jaeger.ServiceName = "notification-service";
        jaeger.Enabled = true;
    })
    .AddRabbitMq()
    .AddJaegerRabbitMqTracing(tracing =>
    {
        tracing.TraceProducers = false;
        tracing.TraceConsumers = true; // This service only consumes
        tracing.TraceMessageProcessing = true;
        tracing.IncludeMessagePayload = false; // Don't include sensitive notification data
        tracing.TagConsumerInfo = true;
        tracing.TagExchange = true;
    });

var app = builder.Build();
app.Run();
```

## Key Features

### 1. Message Producer Tracing

Comprehensive tracing for message publishing:

```csharp
public interface IOrderEventPublisher
{
    Task PublishOrderCreatedAsync(OrderCreated @event);
    Task PublishOrderUpdatedAsync(OrderUpdated @event);
    Task PublishOrderCancelledAsync(OrderCancelled @event);
}

public class OrderEventPublisher : IOrderEventPublisher
{
    private readonly IBusPublisher _busPublisher;
    private readonly ITracer _tracer;
    private readonly ILogger<OrderEventPublisher> _logger;

    public OrderEventPublisher(
        IBusPublisher busPublisher,
        ITracer tracer,
        ILogger<OrderEventPublisher> logger)
    {
        _busPublisher = busPublisher;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task PublishOrderCreatedAsync(OrderCreated @event)
    {
        // Tracing is automatically handled by the library
        // but you can add custom spans for business logic
        using var span = _tracer.BuildSpan("order.created.business-logic")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.customer-id", @event.CustomerId.ToString())
            .WithTag("order.total-amount", @event.TotalAmount.ToString())
            .WithTag("order.item-count", @event.Items.Count.ToString())
            .Start();

        try
        {
            // Business logic before publishing
            _logger.LogInformation("Publishing OrderCreated event for order {OrderId}", @event.Id);

            // Add correlation info to message
            @event.CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
            @event.PublishedAt = DateTime.UtcNow;
            @event.PublishedBy = "order-service";

            // Publish the event - tracing is automatic
            await _busPublisher.PublishAsync(@event, routingKey: "order.created");

            span.SetTag("publishing.status", "success");
            _logger.LogInformation("OrderCreated event published successfully for order {OrderId}", @event.Id);
        }
        catch (Exception ex)
        {
            span.SetTag("publishing.status", "error");
            span.SetTag("error", true);
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message,
                ["stack"] = ex.StackTrace
            });

            _logger.LogError(ex, "Error publishing OrderCreated event for order {OrderId}", @event.Id);
            throw;
        }
    }

    public async Task PublishOrderUpdatedAsync(OrderUpdated @event)
    {
        using var span = _tracer.BuildSpan("order.updated.business-logic")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.customer-id", @event.CustomerId.ToString())
            .WithTag("order.previous-status", @event.PreviousStatus.ToString())
            .WithTag("order.new-status", @event.NewStatus.ToString())
            .Start();

        try
        {
            _logger.LogInformation("Publishing OrderUpdated event for order {OrderId}", @event.Id);

            @event.CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
            @event.PublishedAt = DateTime.UtcNow;
            @event.PublishedBy = "order-service";

            await _busPublisher.PublishAsync(@event, routingKey: "order.updated");

            span.SetTag("publishing.status", "success");
            _logger.LogInformation("OrderUpdated event published successfully for order {OrderId}", @event.Id);
        }
        catch (Exception ex)
        {
            span.SetTag("publishing.status", "error");
            span.SetTag("error", true);
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error publishing OrderUpdated event for order {OrderId}", @event.Id);
            throw;
        }
    }

    public async Task PublishOrderCancelledAsync(OrderCancelled @event)
    {
        using var span = _tracer.BuildSpan("order.cancelled.business-logic")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.customer-id", @event.CustomerId.ToString())
            .WithTag("order.cancellation-reason", @event.Reason)
            .Start();

        try
        {
            _logger.LogInformation("Publishing OrderCancelled event for order {OrderId}", @event.Id);

            @event.CorrelationId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString();
            @event.PublishedAt = DateTime.UtcNow;
            @event.PublishedBy = "order-service";

            await _busPublisher.PublishAsync(@event, routingKey: "order.cancelled");

            span.SetTag("publishing.status", "success");
            _logger.LogInformation("OrderCancelled event published successfully for order {OrderId}", @event.Id);
        }
        catch (Exception ex)
        {
            span.SetTag("publishing.status", "error");
            span.SetTag("error", true);
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error publishing OrderCancelled event for order {OrderId}", @event.Id);
            throw;
        }
    }
}

// Domain events with tracing information
public class OrderCreated : IEvent
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public string ShippingAddress { get; set; } = string.Empty;
    public string BillingAddress { get; set; } = string.Empty;

    // Tracing metadata
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string PublishedBy { get; set; } = string.Empty;
}

public class OrderUpdated : IEvent
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
    public string UpdateReason { get; set; } = string.Empty;
    public decimal? NewTotalAmount { get; set; }

    // Tracing metadata
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string PublishedBy { get; set; } = string.Empty;
}

public class OrderCancelled : IEvent
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime CancelledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal RefundAmount { get; set; }
    public string CancelledBy { get; set; } = string.Empty;

    // Tracing metadata
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string PublishedBy { get; set; } = string.Empty;
}

// Order controller with tracing context
[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IOrderEventPublisher _eventPublisher;
    private readonly ITracer _tracer;

    public OrdersController(
        IOrderService orderService,
        IOrderEventPublisher eventPublisher,
        ITracer tracer)
    {
        _orderService = orderService;
        _eventPublisher = eventPublisher;
        _tracer = tracer;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        using var span = _tracer.BuildSpan("create-order-endpoint")
            .WithTag("customer.id", request.CustomerId.ToString())
            .WithTag("order.item-count", request.Items.Count.ToString())
            .Start();

        try
        {
            var order = await _orderService.CreateOrderAsync(request);

            // Publish event with trace context
            var orderCreatedEvent = new OrderCreated
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CreatedAt = order.CreatedAt,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Items = order.Items.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList(),
                ShippingAddress = order.ShippingAddress,
                BillingAddress = order.BillingAddress
            };

            await _eventPublisher.PublishOrderCreatedAsync(orderCreatedEvent);

            span.SetTag("order.id", order.Id.ToString());
            span.SetTag("operation.status", "success");

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("operation.status", "error");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            throw;
        }
    }
}
```

### 2. Message Consumer Tracing

Comprehensive tracing for message consumption:

```csharp
// Event handlers with automatic tracing
public class OrderCreatedHandler : IEventHandler<OrderCreated>
{
    private readonly IInventoryService _inventoryService;
    private readonly INotificationService _notificationService;
    private readonly ITracer _tracer;
    private readonly ILogger<OrderCreatedHandler> _logger;

    public OrderCreatedHandler(
        IInventoryService inventoryService,
        INotificationService notificationService,
        ITracer tracer,
        ILogger<OrderCreatedHandler> logger)
    {
        _inventoryService = inventoryService;
        _notificationService = notificationService;
        _tracer = tracer;
        _logger = logger;
    }

    // Handler automatically traced by the library
    public async Task HandleAsync(OrderCreated @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-created-handler")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.customer-id", @event.CustomerId.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .WithTag("handler.service", "inventory-service")
            .Start();

        try
        {
            _logger.LogInformation("Processing OrderCreated event for order {OrderId}", @event.Id);

            // Reserve inventory items
            using var inventorySpan = _tracer.BuildSpan("reserve-inventory")
                .AsChildOf(span)
                .WithTag("order.id", @event.Id.ToString())
                .WithTag("item.count", @event.Items.Count.ToString())
                .Start();

            var reservationResults = new List<InventoryReservationResult>();

            foreach (var item in @event.Items)
            {
                using var itemSpan = _tracer.BuildSpan("reserve-inventory-item")
                    .AsChildOf(inventorySpan)
                    .WithTag("product.id", item.ProductId.ToString())
                    .WithTag("quantity", item.Quantity.ToString())
                    .Start();

                try
                {
                    var result = await _inventoryService.ReserveAsync(item.ProductId, item.Quantity);
                    reservationResults.Add(result);

                    itemSpan.SetTag("reservation.status", result.Success ? "success" : "failed");
                    itemSpan.SetTag("reservation.id", result.ReservationId?.ToString() ?? "none");

                    if (!result.Success)
                    {
                        itemSpan.SetTag("error", true);
                        itemSpan.Log(new Dictionary<string, object>
                        {
                            ["event"] = "reservation_failed",
                            ["reason"] = result.FailureReason,
                            ["available_quantity"] = result.AvailableQuantity.ToString()
                        });
                    }
                }
                catch (Exception ex)
                {
                    itemSpan.SetTag("error", true);
                    itemSpan.Log(new Dictionary<string, object>
                    {
                        ["event"] = "error",
                        ["error.kind"] = ex.GetType().Name,
                        ["error.object"] = ex.Message
                    });
                    throw;
                }
            }

            inventorySpan.SetTag("total.reservations", reservationResults.Count.ToString());
            inventorySpan.SetTag("successful.reservations", reservationResults.Count(r => r.Success).ToString());
            inventorySpan.SetTag("failed.reservations", reservationResults.Count(r => !r.Success).ToString());

            // Send confirmation notification
            using var notificationSpan = _tracer.BuildSpan("send-order-confirmation")
                .AsChildOf(span)
                .WithTag("order.id", @event.Id.ToString())
                .WithTag("customer.id", @event.CustomerId.ToString())
                .Start();

            try
            {
                await _notificationService.SendOrderConfirmationAsync(new OrderConfirmationNotification
                {
                    OrderId = @event.Id,
                    CustomerId = @event.CustomerId,
                    TotalAmount = @event.TotalAmount,
                    Items = @event.Items,
                    CorrelationId = @event.CorrelationId
                });

                notificationSpan.SetTag("notification.status", "sent");
                notificationSpan.SetTag("notification.type", "order_confirmation");
            }
            catch (Exception ex)
            {
                notificationSpan.SetTag("error", true);
                notificationSpan.SetTag("notification.status", "failed");
                notificationSpan.Log(new Dictionary<string, object>
                {
                    ["event"] = "error",
                    ["error.kind"] = ex.GetType().Name,
                    ["error.object"] = ex.Message
                });

                // Don't fail the entire handler for notification failures
                _logger.LogError(ex, "Failed to send order confirmation for order {OrderId}", @event.Id);
            }

            span.SetTag("handler.status", "completed");
            span.SetTag("inventory.all-reserved", reservationResults.All(r => r.Success).ToString());

            _logger.LogInformation("Completed processing OrderCreated event for order {OrderId}", @event.Id);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("handler.status", "failed");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message,
                ["stack"] = ex.StackTrace
            });

            _logger.LogError(ex, "Error processing OrderCreated event for order {OrderId}", @event.Id);
            throw;
        }
    }
}

public class OrderUpdatedHandler : IEventHandler<OrderUpdated>
{
    private readonly IOrderService _orderService;
    private readonly INotificationService _notificationService;
    private readonly ITracer _tracer;
    private readonly ILogger<OrderUpdatedHandler> _logger;

    public OrderUpdatedHandler(
        IOrderService orderService,
        INotificationService notificationService,
        ITracer tracer,
        ILogger<OrderUpdatedHandler> logger)
    {
        _orderService = orderService;
        _notificationService = notificationService;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task HandleAsync(OrderUpdated @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-updated-handler")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.customer-id", @event.CustomerId.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .WithTag("order.previous-status", @event.PreviousStatus.ToString())
            .WithTag("order.new-status", @event.NewStatus.ToString())
            .WithTag("handler.service", "notification-service")
            .Start();

        try
        {
            _logger.LogInformation("Processing OrderUpdated event for order {OrderId}", @event.Id);

            // Update internal order status if needed
            if (@event.NewStatus == OrderStatus.Shipped)
            {
                using var shippingSpan = _tracer.BuildSpan("process-shipping-update")
                    .AsChildOf(span)
                    .WithTag("order.id", @event.Id.ToString())
                    .Start();

                await _orderService.UpdateShippingStatusAsync(@event.Id, @event.NewStatus);
                shippingSpan.SetTag("shipping.status", "updated");
            }

            // Send status update notification
            using var notificationSpan = _tracer.BuildSpan("send-status-update-notification")
                .AsChildOf(span)
                .WithTag("order.id", @event.Id.ToString())
                .WithTag("notification.type", "status_update")
                .Start();

            await _notificationService.SendOrderStatusUpdateAsync(new OrderStatusUpdateNotification
            {
                OrderId = @event.Id,
                CustomerId = @event.CustomerId,
                PreviousStatus = @event.PreviousStatus,
                NewStatus = @event.NewStatus,
                UpdateReason = @event.UpdateReason,
                CorrelationId = @event.CorrelationId
            });

            notificationSpan.SetTag("notification.status", "sent");
            span.SetTag("handler.status", "completed");

            _logger.LogInformation("Completed processing OrderUpdated event for order {OrderId}", @event.Id);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("handler.status", "failed");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error processing OrderUpdated event for order {OrderId}", @event.Id);
            throw;
        }
    }
}

public class OrderCancelledHandler : IEventHandler<OrderCancelled>
{
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ITracer _tracer;
    private readonly ILogger<OrderCancelledHandler> _logger;

    public OrderCancelledHandler(
        IInventoryService inventoryService,
        IPaymentService paymentService,
        INotificationService notificationService,
        ITracer tracer,
        ILogger<OrderCancelledHandler> logger)
    {
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _tracer = tracer;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCancelled @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-cancelled-handler")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.customer-id", @event.CustomerId.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .WithTag("cancellation.reason", @event.Reason)
            .WithTag("refund.amount", @event.RefundAmount.ToString())
            .WithTag("handler.service", "refund-service")
            .Start();

        try
        {
            _logger.LogInformation("Processing OrderCancelled event for order {OrderId}", @event.Id);

            // Release inventory reservations
            using var inventorySpan = _tracer.BuildSpan("release-inventory-reservations")
                .AsChildOf(span)
                .WithTag("order.id", @event.Id.ToString())
                .Start();

            await _inventoryService.ReleaseReservationsAsync(@event.Id);
            inventorySpan.SetTag("reservations.status", "released");

            // Process refund
            using var refundSpan = _tracer.BuildSpan("process-refund")
                .AsChildOf(span)
                .WithTag("order.id", @event.Id.ToString())
                .WithTag("refund.amount", @event.RefundAmount.ToString())
                .Start();

            var refundResult = await _paymentService.ProcessRefundAsync(@event.Id, @event.RefundAmount);
            refundSpan.SetTag("refund.status", refundResult.Success ? "success" : "failed");
            refundSpan.SetTag("refund.id", refundResult.RefundId?.ToString() ?? "none");

            if (!refundResult.Success)
            {
                refundSpan.SetTag("error", true);
                refundSpan.Log(new Dictionary<string, object>
                {
                    ["event"] = "refund_failed",
                    ["reason"] = refundResult.FailureReason
                });
            }

            // Send cancellation notification
            using var notificationSpan = _tracer.BuildSpan("send-cancellation-notification")
                .AsChildOf(span)
                .WithTag("order.id", @event.Id.ToString())
                .WithTag("notification.type", "cancellation")
                .Start();

            await _notificationService.SendOrderCancellationAsync(new OrderCancellationNotification
            {
                OrderId = @event.Id,
                CustomerId = @event.CustomerId,
                CancellationReason = @event.Reason,
                RefundAmount = @event.RefundAmount,
                RefundProcessed = refundResult.Success,
                CorrelationId = @event.CorrelationId
            });

            notificationSpan.SetTag("notification.status", "sent");
            span.SetTag("handler.status", "completed");
            span.SetTag("refund.processed", refundResult.Success.ToString());

            _logger.LogInformation("Completed processing OrderCancelled event for order {OrderId}", @event.Id);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("handler.status", "failed");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error processing OrderCancelled event for order {OrderId}", @event.Id);
            throw;
        }
    }
}
```

### 3. Advanced Tracing Patterns

Complex tracing scenarios with correlation and context propagation:

```csharp
// Distributed saga with tracing
public class OrderProcessingSaga : ISaga<OrderCreated, OrderPaymentProcessed, OrderShipped, OrderDelivered>
{
    private readonly ITracer _tracer;
    private readonly ILogger<OrderProcessingSaga> _logger;
    private readonly IPaymentService _paymentService;
    private readonly IShippingService _shippingService;

    public OrderProcessingSaga(
        ITracer tracer,
        ILogger<OrderProcessingSaga> logger,
        IPaymentService paymentService,
        IShippingService shippingService)
    {
        _tracer = tracer;
        _logger = logger;
        _paymentService = paymentService;
        _shippingService = shippingService;
    }

    public async Task HandleAsync(OrderCreated @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-saga-payment-step")
            .WithTag("saga.type", "order_processing")
            .WithTag("saga.step", "payment")
            .WithTag("order.id", @event.Id.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .Start();

        try
        {
            _logger.LogInformation("Starting payment processing for order {OrderId}", @event.Id);

            // Extract parent trace context if available
            if (!string.IsNullOrEmpty(@event.CorrelationId))
            {
                span.SetTag("parent.trace-id", @event.CorrelationId);
            }

            var paymentResult = await _paymentService.ProcessPaymentAsync(@event.Id, @event.TotalAmount);

            span.SetTag("payment.status", paymentResult.Success ? "success" : "failed");
            span.SetTag("payment.id", paymentResult.PaymentId?.ToString() ?? "none");

            if (paymentResult.Success)
            {
                span.SetTag("saga.next-step", "shipping");
                _logger.LogInformation("Payment successful for order {OrderId}, proceeding to shipping", @event.Id);
            }
            else
            {
                span.SetTag("error", true);
                span.SetTag("saga.status", "failed");
                span.Log(new Dictionary<string, object>
                {
                    ["event"] = "payment_failed",
                    ["reason"] = paymentResult.FailureReason
                });

                _logger.LogError("Payment failed for order {OrderId}: {Reason}", @event.Id, paymentResult.FailureReason);
                throw new PaymentFailedException($"Payment failed: {paymentResult.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("saga.status", "error");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error in payment step of order saga for order {OrderId}", @event.Id);
            throw;
        }
    }

    public async Task HandleAsync(OrderPaymentProcessed @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-saga-shipping-step")
            .WithTag("saga.type", "order_processing")
            .WithTag("saga.step", "shipping")
            .WithTag("order.id", @event.OrderId.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .WithTag("payment.id", @event.PaymentId.ToString())
            .Start();

        try
        {
            _logger.LogInformation("Starting shipping processing for order {OrderId}", @event.OrderId);

            if (!string.IsNullOrEmpty(@event.CorrelationId))
            {
                span.SetTag("parent.trace-id", @event.CorrelationId);
            }

            var shippingResult = await _shippingService.CreateShipmentAsync(@event.OrderId);

            span.SetTag("shipping.status", shippingResult.Success ? "success" : "failed");
            span.SetTag("shipment.id", shippingResult.ShipmentId?.ToString() ?? "none");
            span.SetTag("tracking.number", shippingResult.TrackingNumber ?? "none");

            if (shippingResult.Success)
            {
                span.SetTag("saga.next-step", "delivery");
                _logger.LogInformation("Shipment created for order {OrderId} with tracking {TrackingNumber}",
                    @event.OrderId, shippingResult.TrackingNumber);
            }
            else
            {
                span.SetTag("error", true);
                span.SetTag("saga.status", "failed");
                span.Log(new Dictionary<string, object>
                {
                    ["event"] = "shipping_failed",
                    ["reason"] = shippingResult.FailureReason
                });

                _logger.LogError("Shipping failed for order {OrderId}: {Reason}", @event.OrderId, shippingResult.FailureReason);
                throw new ShippingFailedException($"Shipping failed: {shippingResult.FailureReason}");
            }
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("saga.status", "error");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error in shipping step of order saga for order {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandleAsync(OrderShipped @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-saga-tracking-step")
            .WithTag("saga.type", "order_processing")
            .WithTag("saga.step", "tracking")
            .WithTag("order.id", @event.OrderId.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .WithTag("shipment.id", @event.ShipmentId.ToString())
            .WithTag("tracking.number", @event.TrackingNumber)
            .Start();

        try
        {
            _logger.LogInformation("Order {OrderId} has been shipped with tracking {TrackingNumber}",
                @event.OrderId, @event.TrackingNumber);

            if (!string.IsNullOrEmpty(@event.CorrelationId))
            {
                span.SetTag("parent.trace-id", @event.CorrelationId);
            }

            // Start delivery tracking
            await _shippingService.StartDeliveryTrackingAsync(@event.ShipmentId, @event.TrackingNumber);

            span.SetTag("tracking.status", "active");
            span.SetTag("saga.next-step", "delivery");

            _logger.LogInformation("Delivery tracking started for order {OrderId}", @event.OrderId);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("saga.status", "error");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error in tracking step of order saga for order {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandleAsync(OrderDelivered @event, CancellationToken cancellationToken = default)
    {
        using var span = _tracer.BuildSpan("order-saga-completion-step")
            .WithTag("saga.type", "order_processing")
            .WithTag("saga.step", "completion")
            .WithTag("order.id", @event.OrderId.ToString())
            .WithTag("order.correlation-id", @event.CorrelationId)
            .WithTag("delivery.date", @event.DeliveredAt.ToString("O"))
            .Start();

        try
        {
            _logger.LogInformation("Order {OrderId} has been delivered, completing saga", @event.OrderId);

            if (!string.IsNullOrEmpty(@event.CorrelationId))
            {
                span.SetTag("parent.trace-id", @event.CorrelationId);
            }

            // Complete the order processing saga
            span.SetTag("saga.status", "completed");
            span.SetTag("saga.duration", (DateTime.UtcNow - @event.OrderCreatedAt).TotalMinutes.ToString());

            // Calculate and log saga metrics
            var sagaDuration = DateTime.UtcNow - @event.OrderCreatedAt;
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "saga_completed",
                ["total_duration_minutes"] = sagaDuration.TotalMinutes,
                ["payment_to_shipping_duration"] = (@event.ShippedAt - @event.PaymentProcessedAt)?.TotalMinutes ?? 0,
                ["shipping_to_delivery_duration"] = (@event.DeliveredAt - @event.ShippedAt).TotalMinutes
            });

            _logger.LogInformation("Order processing saga completed for order {OrderId} in {Duration} minutes",
                @event.OrderId, sagaDuration.TotalMinutes);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("saga.status", "error");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error in completion step of order saga for order {OrderId}", @event.OrderId);
            throw;
        }
    }
}
```

### 4. Custom Tracing Middleware

Advanced tracing middleware for RabbitMQ operations:

```csharp
// Custom RabbitMQ tracing middleware
public class RabbitMqTracingMiddleware : IRabbitMqMiddleware
{
    private readonly ITracer _tracer;
    private readonly ILogger<RabbitMqTracingMiddleware> _logger;
    private readonly RabbitMqTracingOptions _options;

    public RabbitMqTracingMiddleware(
        ITracer tracer,
        ILogger<RabbitMqTracingMiddleware> logger,
        IOptions<RabbitMqTracingOptions> options)
    {
        _tracer = tracer;
        _logger = logger;
        _options = options.Value;
    }

    public async Task PublishAsync<T>(T message, string routingKey, IModel channel, Func<Task> next) where T : class
    {
        if (!_options.TraceProducers)
        {
            await next();
            return;
        }

        var operationName = $"rabbitmq.publish.{typeof(T).Name.ToLowerInvariant()}";

        using var span = _tracer.BuildSpan(operationName)
            .WithTag("messaging.system", "rabbitmq")
            .WithTag("messaging.operation", "publish")
            .WithTag("messaging.destination_kind", "topic")
            .WithTag("messaging.destination", _options.ExchangeName)
            .WithTag("messaging.routing_key", routingKey)
            .WithTag("messaging.message_type", typeof(T).Name)
            .Start();

        // Inject trace context into message headers
        var headers = new Dictionary<string, object>();
        _tracer.Inject(span.Context, BuiltinFormats.TextMap, new TextMapInjectAdapter(headers));

        // Add custom tags
        if (_options.TagMessage && message != null)
        {
            AddMessageTags(span, message);
        }

        if (_options.TagRoutingKey)
        {
            span.SetTag("messaging.routing_key", routingKey);
        }

        if (_options.TagExchange)
        {
            span.SetTag("messaging.exchange", _options.ExchangeName);
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            await next();

            stopwatch.Stop();

            span.SetTag("messaging.publish_time_ms", stopwatch.ElapsedMilliseconds.ToString());
            span.SetTag("messaging.success", "true");

            _logger.LogDebug("Published message {MessageType} to exchange {Exchange} with routing key {RoutingKey} in {Duration}ms",
                typeof(T).Name, _options.ExchangeName, routingKey, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("messaging.success", "false");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message
            });

            _logger.LogError(ex, "Error publishing message {MessageType} to exchange {Exchange} with routing key {RoutingKey}",
                typeof(T).Name, _options.ExchangeName, routingKey);

            throw;
        }
    }

    public async Task ConsumeAsync<T>(T message, string routingKey, IModel channel, Func<Task> next) where T : class
    {
        if (!_options.TraceConsumers)
        {
            await next();
            return;
        }

        var operationName = $"rabbitmq.consume.{typeof(T).Name.ToLowerInvariant()}";

        // Extract trace context from message headers if available
        ISpanContext? parentContext = null;
        if (message is IHasHeaders messageWithHeaders && messageWithHeaders.Headers != null)
        {
            try
            {
                parentContext = _tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(messageWithHeaders.Headers));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract trace context from message headers");
            }
        }

        var spanBuilder = _tracer.BuildSpan(operationName)
            .WithTag("messaging.system", "rabbitmq")
            .WithTag("messaging.operation", "receive")
            .WithTag("messaging.destination_kind", "queue")
            .WithTag("messaging.message_type", typeof(T).Name);

        if (parentContext != null)
        {
            spanBuilder = spanBuilder.AsChildOf(parentContext);
        }

        using var span = spanBuilder.Start();

        // Add custom tags
        if (_options.TagMessage && message != null)
        {
            AddMessageTags(span, message);
        }

        if (_options.TagRoutingKey)
        {
            span.SetTag("messaging.routing_key", routingKey);
        }

        if (_options.TagConsumerInfo)
        {
            span.SetTag("messaging.consumer_tag", channel.CurrentQueue);
            span.SetTag("messaging.consumer_id", Environment.MachineName);
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            if (_options.TraceMessageProcessing)
            {
                using var processingSpan = _tracer.BuildSpan($"process.{typeof(T).Name.ToLowerInvariant()}")
                    .AsChildOf(span)
                    .WithTag("messaging.processing", "true")
                    .Start();

                await next();

                processingSpan.SetTag("messaging.processing_success", "true");
            }
            else
            {
                await next();
            }

            stopwatch.Stop();

            span.SetTag("messaging.consume_time_ms", stopwatch.ElapsedMilliseconds.ToString());
            span.SetTag("messaging.success", "true");

            _logger.LogDebug("Consumed and processed message {MessageType} from routing key {RoutingKey} in {Duration}ms",
                typeof(T).Name, routingKey, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            span.SetTag("error", true);
            span.SetTag("messaging.success", "false");
            span.Log(new Dictionary<string, object>
            {
                ["event"] = "error",
                ["error.kind"] = ex.GetType().Name,
                ["error.object"] = ex.Message,
                ["stack"] = ex.StackTrace
            });

            _logger.LogError(ex, "Error consuming message {MessageType} from routing key {RoutingKey}",
                typeof(T).Name, routingKey);

            throw;
        }
    }

    private void AddMessageTags<T>(ISpan span, T message) where T : class
    {
        try
        {
            // Add message-specific tags based on message properties
            var messageType = typeof(T);

            // Add correlation ID if available
            var correlationIdProperty = messageType.GetProperty("CorrelationId");
            if (correlationIdProperty != null)
            {
                var correlationId = correlationIdProperty.GetValue(message)?.ToString();
                if (!string.IsNullOrEmpty(correlationId))
                {
                    span.SetTag("messaging.correlation_id", correlationId);
                }
            }

            // Add message ID if available
            var messageIdProperty = messageType.GetProperty("Id");
            if (messageIdProperty != null)
            {
                var messageId = messageIdProperty.GetValue(message)?.ToString();
                if (!string.IsNullOrEmpty(messageId))
                {
                    span.SetTag("messaging.message_id", messageId);
                }
            }

            // Add timestamp if available
            var timestampProperty = messageType.GetProperty("PublishedAt") ?? messageType.GetProperty("CreatedAt");
            if (timestampProperty != null)
            {
                var timestamp = timestampProperty.GetValue(message);
                if (timestamp is DateTime dateTime)
                {
                    span.SetTag("messaging.message_timestamp", dateTime.ToString("O"));
                }
            }

            // Add payload size if enabled
            if (_options.IncludeMessagePayload)
            {
                var json = JsonSerializer.Serialize(message);
                var payloadSize = Encoding.UTF8.GetByteCount(json);
                span.SetTag("messaging.message_payload_size_bytes", payloadSize.ToString());

                if (json.Length <= _options.MaxPayloadLength)
                {
                    // Sanitize sensitive data
                    var sanitizedPayload = SanitizePayload(json);
                    span.SetTag("messaging.message_payload", sanitizedPayload);
                }
                else
                {
                    span.SetTag("messaging.message_payload", "[TRUNCATED - TOO LARGE]");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error adding message tags to span");
        }
    }

    private string SanitizePayload(string payload)
    {
        foreach (var sensitiveHeader in _options.SensitiveHeaders)
        {
            payload = Regex.Replace(payload,
                $"\"{sensitiveHeader}\"\\s*:\\s*\"[^\"]*\"",
                $"\"{sensitiveHeader}\":\"[REDACTED]\"",
                RegexOptions.IgnoreCase);
        }
        return payload;
    }
}

// Text map adapters for trace context injection/extraction
public class TextMapInjectAdapter : ITextMap
{
    private readonly IDictionary<string, object> _headers;

    public TextMapInjectAdapter(IDictionary<string, object> headers)
    {
        _headers = headers;
    }

    public void Set(string key, string value)
    {
        _headers[key] = value;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _headers.Select(h => new KeyValuePair<string, string>(h.Key, h.Value?.ToString() ?? string.Empty)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class TextMapExtractAdapter : ITextMap
{
    private readonly IDictionary<string, object> _headers;

    public TextMapExtractAdapter(IDictionary<string, object> headers)
    {
        _headers = headers;
    }

    public void Set(string key, string value)
    {
        _headers[key] = value;
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _headers.Select(h => new KeyValuePair<string, string>(h.Key, h.Value?.ToString() ?? string.Empty)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Interface for messages with headers
public interface IHasHeaders
{
    IDictionary<string, object>? Headers { get; set; }
}
```

## Configuration Options

### RabbitMQ Tracing Options

```csharp
public class RabbitMqTracingOptions
{
    public bool TraceProducers { get; set; } = true;
    public bool TraceConsumers { get; set; } = true;
    public bool TraceMessageProcessing { get; set; } = true;
    public bool IncludeMessagePayload { get; set; } = false;
    public int MaxPayloadLength { get; set; } = 1000;
    public string[] SensitiveHeaders { get; set; } = Array.Empty<string>();
    public string[] IgnoreQueues { get; set; } = Array.Empty<string>();
    public bool TagMessage { get; set; } = true;
    public bool TagRoutingKey { get; set; } = true;
    public bool TagExchange { get; set; } = true;
    public bool TagConsumerInfo { get; set; } = true;
    public string ExchangeName { get; set; } = string.Empty;
}
```

## API Reference

### Extension Methods

```csharp
public static class ConveyExtensions
{
    public static IConveyBuilder AddJaegerRabbitMqTracing(this IConveyBuilder builder);
    public static IConveyBuilder AddJaegerRabbitMqTracing(this IConveyBuilder builder, Action<RabbitMqTracingOptions> configure);
    public static IConveyBuilder AddJaegerRabbitMqTracing(this IConveyBuilder builder, string sectionName);
}
```

## Best Practices

1. **Use correlation IDs** - Always propagate correlation IDs across service boundaries
2. **Tag appropriately** - Add meaningful tags for filtering and analysis
3. **Monitor trace volume** - Control tracing overhead in high-throughput scenarios
4. **Sanitize sensitive data** - Ensure no sensitive information is logged in traces
5. **Use sampling** - Implement sampling strategies for production environments
6. **Set up alerting** - Configure alerts for trace anomalies and errors
7. **Optimize span names** - Use consistent and meaningful span naming conventions
8. **Monitor performance impact** - Ensure tracing doesn't significantly impact performance

## Troubleshooting

### Common Issues

1. **Missing trace correlation**
   - Verify trace context injection/extraction
   - Check message header propagation
   - Ensure proper parent-child span relationships

2. **High tracing overhead**
   - Implement sampling strategies
   - Reduce payload logging in high-volume scenarios
   - Optimize span creation and tagging

3. **Incomplete traces**
   - Check service connectivity to Jaeger
   - Verify trace export configuration
   - Monitor trace buffer and flush settings

4. **Sensitive data exposure**
   - Review and update sensitive header lists
   - Implement proper payload sanitization
   - Audit trace outputs regularly
