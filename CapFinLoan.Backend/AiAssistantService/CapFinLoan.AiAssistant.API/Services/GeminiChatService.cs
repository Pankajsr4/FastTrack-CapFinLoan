using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CapFinLoan.AiAssistant.API.Models;

namespace CapFinLoan.AiAssistant.API.Services;

/// <summary>
/// Calls Google Gemini API (gemini-1.5-flash) to generate loan assistant replies.
/// Falls back to a rule-based response if the API key is not configured.
/// </summary>
public sealed class GeminiChatService : IAiChatService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly ILogger<GeminiChatService> _logger;

    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Required documents by loan purpose
    private static readonly Dictionary<string, string[]> RequiredDocs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Home Loan"]      = ["Aadhaar", "PAN", "Salary Slips (3 months)", "Bank Statement (6 months)", "Property Documents"],
        ["Education Loan"] = ["Aadhaar", "PAN", "Admission Letter", "Fee Structure", "Income Proof"],
        ["Personal Loan"]  = ["Aadhaar", "PAN", "Salary Slips (3 months)", "Bank Statement (3 months)"],
        ["Car Loan"]       = ["Aadhaar", "PAN", "Salary Slips", "Bank Statement", "Vehicle Quotation"],
        ["Business Loan"]  = ["Aadhaar", "PAN", "Business Registration", "ITR (2 years)", "Bank Statement (12 months)"],
    };

    public GeminiChatService(IHttpClientFactory factory, IConfiguration config, ILogger<GeminiChatService> logger)
    {
        _http   = factory.CreateClient("Gemini");
        _apiKey = config["Gemini:ApiKey"];
        _logger = logger;
    }

    public async Task<string> GetReplyAsync(string userMessage, ApplicationContext? ctx, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey == "YOUR_GEMINI_API_KEY")
        {
            _logger.LogWarning("Gemini API key not configured — using rule-based fallback.");
            return RuleBasedReply(userMessage, ctx);
        }

        var systemPrompt = BuildSystemPrompt(ctx);
        var payload = new
        {
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = systemPrompt + "\n\nUser: " + userMessage } } }
            },
            generationConfig = new { temperature = 0.7, maxOutputTokens = 512 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
        var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync(url, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini API error {Status}: {Body}", response.StatusCode, body);
                return RuleBasedReply(userMessage, ctx);
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "I'm sorry, I couldn't generate a response.";

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini API call failed");
            return RuleBasedReply(userMessage, ctx);
        }
    }

    // ── System prompt ─────────────────────────────────────────────────────────

    private static string BuildSystemPrompt(ApplicationContext? ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are CapFinLoan AI Assistant — a helpful, friendly loan advisor.");
        sb.AppendLine("You help applicants understand their loan application, required documents, and eligibility.");
        sb.AppendLine("IMPORTANT: You do NOT make final approval or rejection decisions. You only guide and assist.");
        sb.AppendLine("Keep responses concise (2-4 sentences). Use ₹ for currency. Be encouraging.");
        sb.AppendLine();

        if (ctx is not null)
        {
            sb.AppendLine("=== Current Application Context ===");
            if (ctx.ApplicationNumber is not null) sb.AppendLine($"Application: {ctx.ApplicationNumber}");
            if (ctx.Status is not null)            sb.AppendLine($"Status: {ctx.Status}");
            if (ctx.RequestedAmount.HasValue)      sb.AppendLine($"Requested Amount: ₹{ctx.RequestedAmount:N0}");
            if (ctx.TenureMonths.HasValue)         sb.AppendLine($"Tenure: {ctx.TenureMonths} months");
            if (ctx.LoanPurpose is not null)       sb.AppendLine($"Purpose: {ctx.LoanPurpose}");
            if (ctx.MonthlyIncome.HasValue)        sb.AppendLine($"Monthly Income: ₹{ctx.MonthlyIncome:N0}");
            if (ctx.ExistingEmiAmount.HasValue)    sb.AppendLine($"Existing EMI: ₹{ctx.ExistingEmiAmount:N0}");
            if (ctx.UploadedDocumentTypes.Count > 0)
                sb.AppendLine($"Uploaded Documents: {string.Join(", ", ctx.UploadedDocumentTypes)}");

            // Eligibility hint
            if (ctx.MonthlyIncome.HasValue && ctx.RequestedAmount.HasValue)
            {
                var maxEligible = ctx.MonthlyIncome.Value * 60;
                if (ctx.RequestedAmount.Value > maxEligible)
                    sb.AppendLine($"Note: Requested amount may exceed typical eligibility (≈ ₹{maxEligible:N0} based on income).");
            }
        }

        return sb.ToString();
    }

    // ── Rule-based fallback ───────────────────────────────────────────────────

    private static string RuleBasedReply(string message, ApplicationContext? ctx)
    {
        var msg = message.ToLowerInvariant();

        // ── Try to extract numbers from the message for EMI calculation ───────
        var numbers = System.Text.RegularExpressions.Regex.Matches(msg, @"\d[\d,]*\.?\d*")
            .Select(m => double.TryParse(m.Value.Replace(",", ""), out var v) ? v : 0)
            .Where(v => v > 0)
            .OrderByDescending(v => v)
            .ToList();

        // ── EMI calculation — detect amount + tenure from message ─────────────
        bool wantsEmi = msg.Contains("emi") || msg.Contains("instalment") || msg.Contains("monthly payment")
                     || msg.Contains("calculate") || (msg.Contains("loan") && msg.Contains("amount"))
                     || (numbers.Count >= 2 && (msg.Contains("year") || msg.Contains("month") || msg.Contains("tenure")));

        if (wantsEmi)
        {
            // Try to get amount and tenure from message or context
            double? amount = null;
            double? tenureMonths = null;

            // Extract from message text
            if (numbers.Count >= 1)
            {
                // Largest number is likely the amount
                amount = numbers.FirstOrDefault(n => n >= 10000);

                // Look for tenure — years or months
                var yearMatch = System.Text.RegularExpressions.Regex.Match(msg, @"(\d+)\s*year");
                var monthMatch = System.Text.RegularExpressions.Regex.Match(msg, @"(\d+)\s*month");
                if (yearMatch.Success && double.TryParse(yearMatch.Groups[1].Value, out var yrs))
                    tenureMonths = yrs * 12;
                else if (monthMatch.Success && double.TryParse(monthMatch.Groups[1].Value, out var mos))
                    tenureMonths = mos;
                else if (numbers.Count >= 2)
                    tenureMonths = numbers.Where(n => n < 500).FirstOrDefault() is double t && t > 0 ? t : null;
            }

            // Fall back to context
            amount ??= ctx?.RequestedAmount.HasValue == true ? (double?)ctx.RequestedAmount.Value : null;
            tenureMonths ??= ctx?.TenureMonths.HasValue == true ? (double?)ctx.TenureMonths.Value : null;

            if (amount.HasValue && tenureMonths.HasValue && tenureMonths > 0)
            {
                var r = 0.01; // 12% annual / 12
                var n = tenureMonths.Value;
                var p = amount.Value;
                var emi = p * r * Math.Pow(1 + r, n) / (Math.Pow(1 + r, n) - 1);
                var total = emi * n;
                var interest = total - p;
                return $"📊 **EMI Calculation**\n" +
                       $"• Loan Amount: ₹{p:N0}\n" +
                       $"• Tenure: {n:N0} months ({n / 12:N1} years)\n" +
                       $"• Interest Rate: ~12% p.a.\n" +
                       $"• **Monthly EMI: ₹{emi:N0}**\n" +
                       $"• Total Payment: ₹{total:N0}\n" +
                       $"• Total Interest: ₹{interest:N0}\n\n" +
                       "Actual rate may vary based on your credit profile.";
            }
            return "📊 To calculate your EMI, please provide your loan amount and tenure. For example: \"Calculate EMI for ₹25,00,000 over 7 years\"";
        }

        // ── Document count/status queries ─────────────────────────────────────
        if ((msg.Contains("how many") || msg.Contains("count") || msg.Contains("approved") || msg.Contains("verified"))
            && (msg.Contains("document") || msg.Contains("doc")))
        {
            var uploaded = ctx?.UploadedDocumentTypes ?? [];
            if (uploaded.Count > 0)
                return $"📎 You have **{uploaded.Count} document(s)** uploaded: {string.Join(", ", uploaded)}. " +
                       "Check the Documents section for their verification status (Pending/Verified/Rejected).";
            return "📎 You haven't uploaded any documents yet. Go to the **Upload** section to add your KYC and income documents.";
        }

        // ── Short follow-up: loan type specific documents ─────────────────────
        if (msg.Length < 40 && (msg.Contains("education") || msg.Contains("home") || msg.Contains("personal")
            || msg.Contains("car") || msg.Contains("business") || msg.Contains("loan")))
        {
            var loanType = msg.Contains("education") ? "Education Loan"
                         : msg.Contains("home")      ? "Home Loan"
                         : msg.Contains("car")       ? "Car Loan"
                         : msg.Contains("business")  ? "Business Loan"
                         : "Personal Loan";
            var required = RequiredDocs.TryGetValue(loanType, out var docs) ? docs : RequiredDocs["Personal Loan"];
            return $"📎 For a **{loanType}**, you need:\n{string.Join("\n", required.Select(d => $"• {d}"))}";
        }

        // ── Document requirements ─────────────────────────────────────────────
        // Only trigger if asking about what documents are needed, not about counts/status
        bool asksAboutDocRequirements = (msg.Contains("document") || msg.Contains("upload") || msg.Contains("kyc") || msg.Contains("paper"))
            && !msg.Contains("how many") && !msg.Contains("approved") && !msg.Contains("verified") && !msg.Contains("count") && !msg.Contains("status");

        if (asksAboutDocRequirements)
        {
            var purpose = ctx?.LoanPurpose ?? "Personal Loan";
            var required = RequiredDocs.TryGetValue(purpose, out var docs)
                ? docs
                : RequiredDocs["Personal Loan"];
            var uploaded = ctx?.UploadedDocumentTypes ?? [];
            var missing  = required.Where(d => !uploaded.Any(u => u.Contains(d.Split(' ')[0], StringComparison.OrdinalIgnoreCase))).ToList();

            return missing.Count == 0
                ? $"✅ Great! All required documents for your {purpose} appear to be uploaded."
                : $"📎 For a **{purpose}**, you need:\n{string.Join("\n", required.Select(d => $"• {d}"))}\n\n" +
                  $"Still missing: **{string.Join(", ", missing)}**";
        }

        // ── Status ────────────────────────────────────────────────────────────
        if (msg.Contains("status") || msg.Contains("progress") || msg.Contains("update") || msg.Contains("where"))
        {
            var status = ctx?.Status ?? "unknown";
            return status switch
            {
                "Draft"        => "📝 Your application is saved as a **Draft**. Complete all sections and submit it to begin review.",
                "Submitted"    => "📤 Your application has been **Submitted** and is awaiting admin review. This typically takes 1-3 business days.",
                "Docs Pending" => "📎 The admin has requested additional documents. Please upload them from the Documents section.",
                "Under Review" => "🔍 Your application is currently **Under Review** by our team. We'll notify you of any updates.",
                "Approved"     => "🎉 **Congratulations!** Your loan has been approved. Our team will contact you for disbursement.",
                "Rejected"     => "❌ Unfortunately your application was not approved this time. You may reapply after 90 days.",
                _              => $"Your application status is: **{status}**."
            };
        }

        // ── Eligibility ───────────────────────────────────────────────────────
        if (msg.Contains("eligib") || msg.Contains("qualify") || msg.Contains("how much") || msg.Contains("maximum"))
        {
            if (ctx?.MonthlyIncome.HasValue == true)
            {
                var max = ctx.MonthlyIncome.Value * 60;
                var emi = ctx.ExistingEmiAmount ?? 0;
                var available = ctx.MonthlyIncome.Value * 0.5m - emi;
                return $"💡 Based on your monthly income of ₹{ctx.MonthlyIncome:N0}:\n" +
                       $"• **Max eligible loan: ₹{max:N0}** (60x monthly income)\n" +
                       $"• Available EMI capacity: ₹{available:N0}/month\n\n" +
                       "Final eligibility depends on credit score and other factors.";
            }
            return "💡 Loan eligibility is typically **50-60x your monthly income**. Please ensure your income details are filled in your application.";
        }

        // ── Greeting ──────────────────────────────────────────────────────────
        if (msg.Contains("hello") || msg.Contains("hi") || msg.Contains("help") || msg.Contains("what can"))
            return "👋 Hello! I'm your **CapFinLoan AI Assistant**. I can help you with:\n" +
                   "• 📎 Required documents for your loan\n" +
                   "• 💡 Loan eligibility calculation\n" +
                   "• 📊 EMI calculation\n" +
                   "• 📋 Application status\n\n" +
                   "What would you like to know?";

        // ── Interest rate ─────────────────────────────────────────────────────
        if (msg.Contains("interest") || msg.Contains("rate") || msg.Contains("%"))
            return "📈 Interest rates at CapFinLoan typically range from **10.5% to 18% p.a.** depending on your credit score, income, and loan type. Home loans tend to have lower rates (~10.5-12%), while personal loans are higher (~14-18%).";

        // ── Processing time ───────────────────────────────────────────────────
        if (msg.Contains("how long") || msg.Contains("time") || msg.Contains("days") || msg.Contains("when"))
            return "⏱️ Typical processing times:\n• Document verification: 1-2 days\n• Credit assessment: 2-3 days\n• Final decision: 3-5 business days\n\nEnsure all documents are uploaded to avoid delays.";

        return "I'm here to help with your loan application! You can ask me about **required documents**, **eligibility**, **EMI calculations**, or **application status**. Try asking: \"Calculate EMI for ₹10 lakh over 5 years\"";
    }
}
