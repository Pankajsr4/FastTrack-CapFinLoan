using CapFinLoan.Document.Application.Interfaces;
using CapFinLoan.Document.Infrastructure.Messaging;
using CapFinLoan.Messaging.Contracts.Configuration;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CapFinLoan.Document.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
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
        //           └── IMessageHandler<T>          (Scoped)
        //                 └── IDocumentRepository    (Scoped)
        //                       └── DocumentDbContext (Scoped)
        services.AddScoped<IMessageHandler<ApplicationStatusChangedEvent>, ApplicationStatusChangedHandler>();

        // ── BackgroundService consumers ───────────────────────────────────────
        services.AddHostedService<RabbitMqConsumer<ApplicationStatusChangedEvent>>();

        // ── MassTransit (bus infrastructure only) ─────────────────────────────
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((_, cfg) =>
            {
                cfg.Host(rabbit.Host, rabbit.VirtualHost, h =>
                {
                    h.Username(rabbit.Username);
                    h.Password(rabbit.Password);
                });
            });
        });

        return services;
    }
}
