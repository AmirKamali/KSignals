using System;

namespace KSignal.API.Services;

public sealed class RabbitMqUnavailableException : Exception
{
    public RabbitMqUnavailableException(string message) : base(message)
    {
    }

    public RabbitMqUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
