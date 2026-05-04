namespace CapFinLoan.Application.Application.Contracts.Responses;

public class ApplicationStatusHistoryResponse
{
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public Guid ChangedByUserId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}