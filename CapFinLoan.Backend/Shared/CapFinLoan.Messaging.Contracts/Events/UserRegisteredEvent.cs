namespace CapFinLoan.Messaging.Contracts.Events;

public record UserRegisteredEvent
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime RegisteredAtUtc { get; init; }
}
