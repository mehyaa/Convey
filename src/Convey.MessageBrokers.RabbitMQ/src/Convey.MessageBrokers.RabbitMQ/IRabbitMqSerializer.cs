using System;

namespace Convey.MessageBrokers.RabbitMQ;

public interface IRabbitMqSerializer
{
    string ContentEncoding { get; }
    string ContentType { get; }
    byte[] Serialize(object value);
    object Deserialize(byte[] value, Type type);
    object Deserialize(byte[] value);
}