using CapFinLoan.Messaging.Contracts.Configuration;
using CapFinLoan.Messaging.Contracts.Events;
using CapFinLoan.Messaging.Contracts.Messaging;
using CapFinLoan.Notification.Application.Interfaces;
using CapFinLoan.Notification.Application.Services;
using CapFinLoan.Notification.Infrastructure.Data;
using CapFinLoan.Notification.Infrastructure.Messaging;
using CapFinLoan.Notification.Infrastructure.Repositories;
using CapFinLoan.Notification.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CapFinLoan.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("CapFinLoanDb")));

        services.AddScoped<INotificationRepository, NotificationRepository>();

        // ── RabbitMQ ──────────────────────────────────────────────────────────
        services.Configure<RabbitMQSettings>(
            options => configuration.GetSection(RabbitMQSettings.SectionName).Bind(options));

        var rabbit = new RabbitMQSettings();
        configuration.GetSection(RabbitMQSettings.SectionName).Bind(rabbit);

        if (string.IsNullOrWhiteSpace(rabbit.Host))
            throw new InvalidOperationException("RabbitMQ:Host is not configured.");

        services.AddSingleton<RabbitMqConnectionFactory>();
        services.AddSingleton<RabbitMqPublisher>();

        // ── SignalR pusher ────────────────────────────────────────────────────
        services.AddScoped<INotificationPusher, SignalRNotificationPusher>();

        // ── Notification service ──────────────────────────────────────────────
        services.AddScoped<INotificationService, NotificationService>();

        // ── Message handlers ──────────────────────────────────────────────────
        services.AddScoped<IMessageHandler<ApplicationSubmittedEvent>,     ApplicationSubmittedHandler>();
        services.AddScoped<IMessageHandler<ApplicationStatusChangedEvent>, ApplicationStatusChangedHandler>();

        // ── BackgroundService consumers ───────────────────────────────────────
        services.AddHostedService<RabbitMqConsumer<ApplicationSubmittedEvent>>();
        services.AddHostedService<RabbitMqConsumer<ApplicationStatusChangedEvent>>();

        return services;
    }
}
