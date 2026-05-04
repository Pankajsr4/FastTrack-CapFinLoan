using System.Text;
using CapFinLoan.Api.Shared;
using CapFinLoan.Document.API.Hubs;
using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Document.Application.Services;
using CapFinLoan.Document.Persistence.Data;
using FluentValidation;
using FluentValidation.AspNetCore;
using CapFinLoan.Document.Infrastructure;
using CapFinLoan.Document.Infrastructure.Storage;
using CapFinLoan.Document.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Bootstrap logger — captures startup errors before the full logger is configured.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Load serilog.json before UseSerilog so it can read sink configuration ─
    builder.Configuration.AddJsonFile("serilog.json", optional: true, reloadOnChange: true);

    // ── Serilog — replaces the default Microsoft logging pipeline ────────────
    // Reads from serilog.json: console + rolling file sinks, per-namespace levels.
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes  = true;
        options.ValidateOnBuild = true;
    });

    builder.Services.AddDbContext<DocumentDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("CapFinLoanDb")));

    builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
    builder.Services.AddScoped<IDocumentService, DocumentService>();

    builder.Services.AddInfrastructure(builder.Configuration);

    var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
    builder.Services.AddSingleton<IFileStorageService>(new LocalFileStorageService(uploadsPath));

    var jwtSection = builder.Configuration.GetSection("Jwt");
    var secretKey  = jwtSection["Key"]      ?? throw new InvalidOperationException("JWT key is missing.");
    var issuer     = jwtSection["Issuer"]   ?? throw new InvalidOperationException("JWT issuer is missing.");
    var audience   = jwtSection["Audience"] ?? throw new InvalidOperationException("JWT audience is missing.");
    var key        = Encoding.UTF8.GetBytes(secretKey);

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = issuer,
                ValidAudience            = audience,
                IssuerSigningKey         = new SymmetricSecurityKey(key),
                ClockSkew                = TimeSpan.FromMinutes(1),
                RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
                NameClaimType            = System.Security.Claims.ClaimTypes.Name
            };
        });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();

    // ── FluentValidation ──────────────────────────────────────────────────────
    // Registers all validators in this assembly and wires them into the model
    // validation pipeline. Invalid requests return 400 with structured errors
    // before the action method is ever called.
    builder.Services.AddFluentValidationAutoValidation()
                    .AddValidatorsFromAssemblyContaining<Program>();

    // ── Health checks ─────────────────────────────────────────────────────────
    // GET /health        — full report (DB + RabbitMQ)
    // GET /health/live   — liveness probe (always 200 if process is running)
    // GET /health/ready  — readiness probe (DB + RabbitMQ must be healthy)
    builder.Services.Configure<RabbitMqHealthCheckOptions>(
        builder.Configuration.GetSection("RabbitMQ"));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<DocumentDbContext>(
            name:    "database",
            tags:    ["ready", "db"])
        .AddCheck<RabbitMqHealthCheck>(
            name:    "rabbitmq",
            tags:    ["ready", "rabbitmq"]);

    // ── SignalR ───────────────────────────────────────────────────────────────
    builder.Services.AddSignalR();
    builder.Services.AddScoped<IDocumentStatusNotifier, DocumentStatusNotifier>();
    // ── CORS — allow React dev server to connect (including WebSocket upgrade) ─
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactDevServer", policy =>
            policy.WithOrigins(
                    "http://localhost:3000",   // Vite dev server
                    "http://localhost:5173")   // Vite default fallback
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials());        // required for SignalR
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "CapFinLoan Document API",
            Version = "1.0"
        });
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name        = "Authorization",
            Type        = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            In          = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter 'Bearer' [space] and then your valid JWT token."
        });
        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id   = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // ── Log: application started ──────────────────────────────────────────────
    app.Logger.LogInformation("DocumentService starting up in {Environment} environment.",
        app.Environment.EnvironmentName);

    app.UseSwagger();
    app.UseSwaggerUI();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DocumentDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    app.UseStaticFiles();
    app.UseSerilogRequestLogging();
    app.UseGlobalExceptionHandler();   // ← must be before UseCors/UseAuth
    app.UseCors("ReactDevServer");          // must be before UseAuthentication
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHub<DocumentStatusHub>("/hubs/document-status");
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
    });
    app.MapHealthChecks("/health/live",  new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate      = r => r.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
    });
    app.MapGet("/", () => "Document Service running");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "DocumentService terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
