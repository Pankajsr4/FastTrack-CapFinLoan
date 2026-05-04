using System.Text;
using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Application.Services;
using CapFinLoan.Admin.Infrastructure;
using CapFinLoan.Admin.Infrastructure.Services;
using CapFinLoan.Admin.Persistence.Data;
using CapFinLoan.Admin.Persistence.Repositories;
using CapFinLoan.Api.Shared;
using FluentValidation;
using FluentValidation.AspNetCore;
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

    builder.Configuration.AddJsonFile("serilog.json", optional: true, reloadOnChange: true);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes  = true;
        options.ValidateOnBuild = true;
    });

    builder.Services.AddDbContext<AdminDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("CapFinLoanDb"));
        options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddScoped<IAdminLoanApplicationRepository, AdminLoanApplicationRepository>();
    builder.Services.AddScoped<IAdminLoanApplicationService, AdminLoanApplicationService>();
    builder.Services.AddScoped<IReportService, ReportService>();
    builder.Services.AddScoped<IAdminUserService, AdminUserService>();
    builder.Services.AddSingleton<IEmiCalculatorService, EmiCalculatorService>();

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

    builder.Services.AddHttpClient("AuthServiceClient", c =>
    {
        var url = builder.Configuration["AuthService:BaseUrl"] ?? "http://localhost:5021";
        c.BaseAddress = new Uri(url);
    });
    builder.Services.AddHttpClient("DocumentServiceClient", c =>
    {
        // In Docker: DocumentService__BaseUrl = http://document-service:8080
        // Locally:   falls back to http://localhost:5023
        var url = builder.Configuration["DocumentService:BaseUrl"] ?? "http://localhost:5023";
        c.BaseAddress = new Uri(url);
    });
    builder.Services.AddHttpClient("ApplicationServiceClient", c =>
    {
        // In Docker: ApplicationService__BaseUrl = http://application-service:8080
        var url = builder.Configuration["ApplicationService:BaseUrl"] ?? "http://localhost:5022";
        c.BaseAddress = new Uri(url);
    });

    builder.Services.AddAuthorization();
    builder.Services.AddControllers();

    builder.Services.AddFluentValidationAutoValidation()
                    .AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.Configure<RabbitMqHealthCheckOptions>(
        builder.Configuration.GetSection("RabbitMQ"));

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AdminDbContext>(
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
            Title   = "CapFinLoan Admin API",
            Version = "1.0"
        });
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name         = "Authorization",
            Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme       = "bearer",
            BearerFormat = "JWT",
            In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description  = "Enter 'Bearer' [space] and then your valid JWT token."
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

    app.Logger.LogInformation("AdminService starting up in {Environment} environment.",
        app.Environment.EnvironmentName);

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseSerilogRequestLogging();
    app.UseGlobalExceptionHandler();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate      = r => r.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
    });

    app.MapGet("/", () => "Admin Service running");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AdminService terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}
