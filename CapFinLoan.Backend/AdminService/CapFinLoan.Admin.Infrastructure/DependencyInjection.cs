using CapFinLoan.Admin.Application.Interfaces;
using CapFinLoan.Admin.Application.Services;
using CapFinLoan.Admin.Infrastructure.Messaging;
using CapFinLoan.Admin.Infrastructure.Services;
using CapFinLoan.Admin.Persistence.Repositories;
using CapFinLoan.Api.Shared.Caching;
using CapFinLoan.Messaging.Contracts.Configuration;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CapFinLoan.Admin.Infrastructure;

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

        // Scoped: IEventPublisher wraps RabbitMqPublisher but is resolved per-request
        // so it can carry request-scoped context (correlation ID, etc.) in future.
        services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();

        // ── Application services resolved inside message handler scopes ───────
        // IDocumentProcessingService and its dependencies are Scoped.
        // DocumentUploadedHandler resolves them via IServiceScopeFactory.CreateAsyncScope()
        // — each message gets its own AdminDbContext and unit of work.
        // Other handlers receive their dependencies via constructor injection
        // from the scope created by RabbitMqConsumer<T>.
        services.AddScoped<IDocumentProcessingRepository, DocumentProcessingRepository>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

        // ── Message handlers ──────────────────────────────────────────────────
        // Registered as Scoped — resolved from a fresh scope per message delivery.
        //
        // DocumentUploadedHandler injects IServiceScopeFactory directly and creates
        // its own inner scope to resolve IDocumentProcessingService. This makes the
        // scope boundary explicit at the call site.
        //
        // Lifetime chain for DocumentUploadedEvent:
        //   RabbitMqConsumer<DocumentUploadedEvent>  (Singleton)
        //     └── IServiceScopeFactory.CreateAsyncScope()          [outer scope]
        //           └── DocumentUploadedHandler                    (Scoped)
        //                 └── IServiceScopeFactory.CreateAsyncScope() [inner scope]
        //                       └── IDocumentProcessingService     (Scoped)
        //                             └── IDocumentProcessingRepository (Scoped)
        //                                   └── AdminDbContext     (Scoped)
        //
        // Lifetime chain for other handlers:
        //   RabbitMqConsumer<T>  (Singleton)
        //     └── IServiceScopeFactory.CreateAsyncScope()
        //           └── IMessageHandler<T>  (Scoped, constructor-injected deps)
        services.AddScoped<IMessageHandler<ApplicationSubmittedEvent>, ApplicationSubmittedHandler>();
        services.AddScoped<IMessageHandler<DocumentUploadedEvent>,     DocumentUploadedHandler>();
        services.AddScoped<IMessageHandler<DocumentVerifiedEvent>,     DocumentVerifiedHandler>();

        // ── BackgroundService consumers ───────────────────────────────────────
        // AddHostedService registers as Singleton (host requirement).
        // They MUST NOT hold scoped dependencies directly — they use IServiceScopeFactory.
        services.AddHostedService<RabbitMqConsumer<ApplicationSubmittedEvent>>();
        services.AddHostedService<RabbitMqConsumer<DocumentUploadedEvent>>();
        services.AddHostedService<RabbitMqConsumer<DocumentVerifiedEvent>>();

        return services;
    }
}
