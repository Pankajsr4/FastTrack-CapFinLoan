namespace CapFinLoan.Messaging.Contracts.Events;

public record DocumentVerifiedEvent
{
    public Guid DocumentId { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid UserId { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public bool IsVerified { get; init; }
    public string? Remarks { get; init; }
    public Guid VerifiedByUserId { get; init; }
    public DateTime VerifiedAtUtc { get; init; }
}
