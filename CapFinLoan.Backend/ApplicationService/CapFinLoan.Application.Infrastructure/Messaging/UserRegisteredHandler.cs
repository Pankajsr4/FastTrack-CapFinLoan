using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Logging;

namespace CapFinLoan.Application.Infrastructure.Messaging;

public sealed class UserRegisteredHandler : IMessageHandler<UserRegisteredEvent>
{
    private readonly ILogger<UserRegisteredHandler> _logger;

    public UserRegisteredHandler(ILogger<UserRegisteredHandler> logger)
    {
        _logger = logger;
    }

    public Task<MessageAcknowledgment> HandleAsync(UserRegisteredEvent message, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[ApplicationService] UserRegistered — User: {FullName} ({Email}), Role: {Role}, RegisteredAt: {RegisteredAt:u}",
            message.FullName, message.Email, message.Role, message.RegisteredAtUtc);

        return Task.FromResult(MessageAcknowledgment.Ack());
    }
}
