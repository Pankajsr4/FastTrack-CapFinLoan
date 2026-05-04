using System.Security.Claims;
using CapFinLoan.AiAssistant.API.Models;
using CapFinLoan.AiAssistant.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapFinLoan.AiAssistant.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IAiChatService _ai;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IAiChatService ai, ILogger<ChatController> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    /// <summary>Send a message to the AI loan assistant.</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new ChatResponse { Success = false, Error = "Message cannot be empty." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        _logger.LogInformation("[AI Chat] User {UserId}: {Message}", userId, request.Message);

        try
        {
            var reply = await _ai.GetReplyAsync(request.Message, request.ApplicationContext, cancellationToken);
            return Ok(new ChatResponse { Reply = reply });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AI Chat] Error for user {UserId}", userId);
            return Ok(new ChatResponse
            {
                Reply = "I'm having trouble connecting right now. Please try again in a moment.",
                Success = false
            });
        }
    }

    /// <summary>Health check — no auth required.</summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new { status = "AI Assistant running" });
}
