using CapFinLoan.Auth.Application.Interfaces;
using MassTransit;

namespace CapFinLoan.Auth.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public RabbitMqEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        await _publishEndpoint.Publish(message, cancellationToken);
        Console.WriteLine($"[RabbitMQ] Published {typeof(T).Name}");
    }
}
