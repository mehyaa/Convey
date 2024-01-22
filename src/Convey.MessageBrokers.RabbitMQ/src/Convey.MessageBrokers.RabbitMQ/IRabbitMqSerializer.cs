using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IRabbitMqSerializer
{
    string ContentEncoding { get; }
    string ContentType { get; }
    ReadOnlySpan<byte> Serialize(object value);
    object Deserialize(ReadOnlySpan<byte> value, Type type);
    object Deserialize(ReadOnlySpan<byte> value);
}