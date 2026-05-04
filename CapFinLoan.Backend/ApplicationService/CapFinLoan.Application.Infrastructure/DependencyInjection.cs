using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Application.Application.Interfaces;
using CapFinLoan.Application.Infrastructure.Messaging;
using CapFinLoan.Messaging.Contracts.Configuration;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CapFinLoan.Application.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Redis caching ─────────────────────────────────────────────────────
        services.AddRedisCaching(configuration);

        // ── RabbitMQ settings ─────────────────────────────────────────────────
        services.Configure<RabbitMQSettings>(
            configuration.GetSection(RabbitMQSettings.SectionName));

        var rabbit = new RabbitMQSettings();
        configuration.GetSection(RabbitMQSettings.SectionName).Bind(rabbit);

        if (string.IsNullOrWhiteSpace(rabbit.Host))
            throw new InvalidOperationException("RabbitMQ:Host is not configured.");

        // ── Publisher ─────────────────────────────────────────────────────────
        // Singleton: one AMQP connection + stateless publisher shared across the process.
        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<RabbitMqPublisher>();

        // Scoped: resolved per-request / per-message scope.
        services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();

        // ── Message handlers ──────────────────────────────────────────────────
        // Scoped — resolved from a fresh IServiceScopeFactory scope per message.
        // Each handler gets its own DbContext, repository, and unit of work.
        //
        // Lifetime chain per message:
        //   RabbitMqConsumer<T> (Singleton)
        //     └── IServiceScopeFactory.CreateAsyncScope()
        //           └── IMessageHandler<T>               (Scoped)
        //                 └── ILoanApplicationRepository  (Scoped)
        //                       └── ApplicationDbContext  (Scoped)
        services.AddScoped<IMessageHandler<ApplicationStatusChangedEvent>, ApplicationStatusChangedHandler>();
        services.AddScoped<IMessageHandler<DocumentUploadedEvent>,         DocumentUploadedHandler>();
        services.AddScoped<IMessageHandler<DocumentVerifiedEvent>,         DocumentVerifiedHandler>();
        services.AddScoped<IMessageHandler<UserRegisteredEvent>,           UserRegisteredHandler>();

        // ── BackgroundService consumers ───────────────────────────────────────
        services.AddHostedService<RabbitMqConsumer<ApplicationStatusChangedEvent>>();
        services.AddHostedService<RabbitMqConsumer<DocumentUploadedEvent>>();
        services.AddHostedService<RabbitMqConsumer<DocumentVerifiedEvent>>();
        services.AddHostedService<RabbitMqConsumer<UserRegisteredEvent>>();

        return services;
    }
}
