using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Convey.MessageBrokers.RabbitMQ.Serializers;

public sealed class DefaultRabbitMqSerializer : IRabbitMqSerializer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public DefaultRabbitMqSerializer(JsonSerializerOptions jsonSerializerOptions = null)
    {
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public Task<byte[]> SerializeAsync<T>(T value, string contentType, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(value);

        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (contentType == "application/json")
        {
            return Task.FromResult(JsonSerializer.SerializeToUtf8Bytes(value, _jsonSerializerOptions));
        }

        throw new NotSupportedException($"Content type '{contentType}' is not supported");
    }

    public Task<object> DeserializeAsync(byte[] value, Type type, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (contentType == "application/json")
        {
            return Task.FromResult(JsonSerializer.Deserialize(value, type, _jsonSerializerOptions));
        }

        throw new NotSupportedException($"Content type '{contentType}' is not supported");
    }
}