using System.Text;
using CapFinLoan.Notification.Infrastructure;
using CapFinLoan.Notification.Infrastructure.Data;
using CapFinLoan.Notification.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, _, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // ── JWT ───────────────────────────────────────────────────────────────────
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

            // Allow JWT via query string for SignalR
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var token = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) &&
                        ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        ctx.Token = token;
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = ctx =>
                {
                    Log.Warning("[JWT] Authentication failed: {Error}", ctx.Exception.Message);
                    return Task.CompletedTask;
                },
                OnChallenge = ctx =>
                {
                    Log.Warning("[JWT] Challenge issued — error: {Error}, description: {Desc}",
                        ctx.Error, ctx.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();
    builder.Services.AddSignalR();
    builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
        p.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5173",
                "http://localhost:5000",
                "http://frontend:80")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));

    // ── Infrastructure (DB + RabbitMQ + SignalR pusher + notification service) ─
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    // ── Auto-migrate DB ───────────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<NotificationHub>("/hubs/notifications");
    app.MapGet("/", () => "Notification Service running");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly.");
}
finally
{
    await Log.CloseAndFlushAsync();
}
