namespace CapFinLoan.Application.Application.Contracts.Responses;

public class LoanApplicationStatusResponse
{
    public Guid Id { get; set; }
    public string ApplicationNumber { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public IReadOnlyCollection<ApplicationStatusHistoryResponse> Timeline { get; set; } = Array.Empty<ApplicationStatusHistoryResponse>();
}