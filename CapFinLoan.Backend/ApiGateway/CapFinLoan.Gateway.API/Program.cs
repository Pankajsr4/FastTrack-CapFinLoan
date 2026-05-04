using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MMLib.SwaggerForOcelot.DependencyInjection;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ── Load ocelot config ────────────────────────────────────────────────────────
// In Docker: OCELOT_CONFIG_PATH = /app/ocelot.Docker.json (service names)
// Locally:   falls back to ocelot.json (localhost ports)
var ocelotPath = Environment.GetEnvironmentVariable("OCELOT_CONFIG_PATH") ?? "ocelot.json";
builder.Configuration.AddJsonFile(ocelotPath, optional: false, reloadOnChange: true);

// ── JWT validation ────────────────────────────────────────────────────────────
var jwt = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(
    jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is missing."));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwt["Issuer"],
            ValidAudience            = jwt["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(key),
            ClockSkew                = TimeSpan.FromMinutes(1),
            RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
            NameClaimType            = System.Security.Claims.ClaimTypes.Name,
        };

        // SignalR WebSocket: token comes via ?access_token= query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/gateway/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ── Ocelot + Swagger aggregation ─────────────────────────────────────────────
builder.Services.AddOcelot(builder.Configuration);
builder.Services.AddSwaggerForOcelot(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
// IMPORTANT: MapGet / UseSwaggerForOcelotUI MUST come before UseOcelot()
// because UseOcelot() is terminal — it handles everything not matched above.

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// UseRouting must come before UseOcelot so MapGet endpoints are matched first
app.UseRouting();

// Root health/info endpoint — resolved by endpoint routing before Ocelot sees it
app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/", () => Results.Ok(new
    {
        service = "CapFinLoan API Gateway",
        status  = "running",
        swagger = "/swagger",
        routes  = new[]
        {
            "POST /gateway/auth/signup   → AuthService",
            "POST /gateway/auth/login    → AuthService",
            "ANY  /gateway/applications/ → ApplicationService",
            "ANY  /gateway/documents/    → DocumentService",
            "ANY  /gateway/admin/        → AdminService",
            "WS   /gateway/hubs/         → DocumentService (SignalR)",
        }
    }));

    endpoints.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "Gateway" }));
});

// Swagger UI — must be before UseOcelot
app.UseSwaggerForOcelotUI(options =>
{
    options.PathToSwaggerGenerator = "/swagger/docs";
});

// Ocelot — terminal middleware, handles all /gateway/* routing
await app.UseOcelot();

app.Run();
