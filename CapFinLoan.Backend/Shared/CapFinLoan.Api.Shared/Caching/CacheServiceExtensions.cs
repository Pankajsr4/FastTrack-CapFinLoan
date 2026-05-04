using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CapFinLoan.Api.Shared.Caching;

public static class CacheServiceExtensions
{
    public static IServiceCollection AddRedisCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));

        var settings = new RedisSettings();
        configuration.GetSection(RedisSettings.SectionName).Bind(settings);

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(settings.ConnectionString));

        services.AddSingleton<ICacheService, RedisCacheService>();

        return services;
    }
}
