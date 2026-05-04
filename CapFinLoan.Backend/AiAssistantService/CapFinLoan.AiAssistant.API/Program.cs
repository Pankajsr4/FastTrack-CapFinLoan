using System.Text;
using CapFinLoan.AiAssistant.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, _, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

    // ── JWT (same key as all other services) ─────────────────────────────────
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var key = Encoding.UTF8.GetBytes(
        jwtSection["Key"] ?? throw new InvalidOperationException("JWT key missing."));

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwtSection["Issuer"],
                ValidAudience            = jwtSection["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(key),
                ClockSkew                = TimeSpan.FromMinutes(1),
                RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
                NameClaimType            = System.Security.Claims.ClaimTypes.Name
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();

    // ── Gemini HTTP client ────────────────────────────────────────────────────
    builder.Services.AddHttpClient("Gemini");
    builder.Services.AddScoped<IAiChatService, GeminiChatService>();

    // ── CORS — allow gateway ──────────────────────────────────────────────────
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapGet("/", () => "AI Assistant Service running");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AI Assistant Service terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
