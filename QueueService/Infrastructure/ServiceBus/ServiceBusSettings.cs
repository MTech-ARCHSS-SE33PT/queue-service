namespace QueueService.Infrastructure.ServiceBus;

public sealed class ServiceBusSettings
{
    public string? ConnectionString { get; init; }
    public string? TopicName { get; init; }
    public string? SubscriptionName { get; init; }
}

