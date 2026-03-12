using Microsoft.Extensions.Configuration;

namespace QueueService.Infrastructure;

internal static class ServiceBusConfig
{
    public static string? GetConnectionString(IConfiguration configuration) =>
        configuration["ServiceBus:ConnectionString"];

    public static string? GetTopic(IConfiguration configuration) =>
        configuration["ServiceBus:Topic"] ?? configuration["ServiceBus:TopicName"];

    public static string? GetSubscription(IConfiguration configuration) =>
        configuration["ServiceBus:Subscription"];
}

