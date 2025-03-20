using Convey.Types;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ.Initializers;

public class RabbitMqExchangeInitializer : IInitializer
{
    private const string DefaultType = "topic";

    private readonly IConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqExchangeInitializer> _logger;

    private readonly bool _loggerEnabled;

    public RabbitMqExchangeInitializer(
        ProducerConnection connection,
        RabbitMqOptions options,
        ILogger<RabbitMqExchangeInitializer> logger)
    {
        _connection = connection.Connection;
        _options = options;
        _logger = logger;

        _loggerEnabled = _options.Logger?.Enabled == true;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var exchanges =
            AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsDefined(typeof(MessageAttribute), false))
                .Select(t => t.GetCustomAttribute<MessageAttribute>()?.Exchange)
                .Distinct()
                .ToArray();

        await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        if (_options.Exchange?.Declare == true)
        {
            Log(_options.Exchange.Name, _options.Exchange.Type);

            await channel.ExchangeDeclareAsync(
                _options.Exchange.Name,
                _options.Exchange.Type,
                _options.Exchange.Durable,
                _options.Exchange.AutoDelete,
                cancellationToken: cancellationToken);

            if (_options.DeadLetter?.Enabled is true &&
                _options.DeadLetter?.Declare is true)
            {
                await channel.ExchangeDeclareAsync(
                    $"{_options.DeadLetter.Prefix}{_options.Exchange.Name}{_options.DeadLetter.Suffix}",
                    ExchangeType.Direct,
                    _options.Exchange.Durable,
                    _options.Exchange.AutoDelete,
                    cancellationToken: cancellationToken);
            }
        }

        foreach (var exchange in exchanges)
        {
            if (exchange.Equals(_options.Exchange?.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            Log(exchange, DefaultType);

            await channel.ExchangeDeclareAsync(exchange, DefaultType, true, cancellationToken: cancellationToken);
        }

        await channel.CloseAsync(cancellationToken: cancellationToken);
    }

    private void Log(string exchange, string type)
    {
        if (!_loggerEnabled)
        {
            return;
        }

        _logger.LogInformation("Declaring an exchange: '{Exchange}', type: '{Type}'", exchange, type);
    }
}