namespace QueueService.Events;

public interface IEventPublisher
{
    Task PublishAsync<T>(string eventType, T payload);
}
