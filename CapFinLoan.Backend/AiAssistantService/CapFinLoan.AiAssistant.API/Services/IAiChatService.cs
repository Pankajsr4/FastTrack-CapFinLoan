using CapFinLoan.AiAssistant.API.Models;

namespace CapFinLoan.AiAssistant.API.Services;

public interface IAiChatService
{
    Task<string> GetReplyAsync(string userMessage, ApplicationContext? context, CancellationToken ct = default);
}
