namespace CapFinLoan.AiAssistant.API.Models;

public sealed class ChatResponse
{
    public string Reply { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public string? Error { get; init; }
}
