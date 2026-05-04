namespace CapFinLoan.AiAssistant.API.Models;

public sealed class ChatRequest
{
    /// <summary>The user's message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Optional: application context to give the AI.</summary>
    public ApplicationContext? ApplicationContext { get; init; }
}

public sealed class ApplicationContext
{
    public string? ApplicationNumber { get; init; }
    public string? Status { get; init; }
    public decimal? RequestedAmount { get; init; }
    public int? TenureMonths { get; init; }
    public string? LoanPurpose { get; init; }
    public decimal? MonthlyIncome { get; init; }
    public decimal? ExistingEmiAmount { get; init; }
    public List<string> UploadedDocumentTypes { get; init; } = [];
}
