using System.Text;
using CapFinLoan.Api.Shared;
using CapFinLoan.Application.Application.Interfaces;
using CapFinLoan.Application.Application.Services;
using CapFinLoan.Application.Infrastructure;
using CapFinLoan.Application.Persistence.Data;
using CapFinLoan.Application.Persistence.Repositories;
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
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes  = true;
        options.ValidateOnBuild = true;
    });

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("CapFinLoanDb"));
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();
    builder.Services.AddScoped<ILoanApplicationService, LoanApplicationService>();

    builder.Services.AddInfrastructure(builder.Configuration);

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

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.Configure<RabbitMqHealthCheckOptions>(
        builder.Configuration.GetSection("RabbitMQ"));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<ApplicationDbContext>(
            name: "database",
            tags: ["ready", "db"])
        .AddCheck<RabbitMqHealthCheck>(
            name: "rabbitmq",
            tags: ["ready", "rabbitmq"]);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "CapFinLoan Application API",
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
    app.Logger.LogInformation("ApplicationService starting up in {Environment} environment.",
        app.Environment.EnvironmentName);

    app.UseSwagger();
    app.UseSwaggerUI();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        try { await dbContext.Database.MigrateAsync(); }
        catch (Exception ex) when (ex.Message.Contains("already") || ex.Message.Contains("2714"))
        {
            app.Logger.LogWarning("Migration skipped — tables already exist: {Msg}", ex.Message.Split('\n')[0]);
        }
    }

    app.UseSerilogRequestLogging();
    app.UseGlobalExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
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
    app.MapGet("/", () => "Application Service running");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ApplicationService terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
