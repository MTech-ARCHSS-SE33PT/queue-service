namespace QueueService.Events;

public interface IEventBus
{
    void Subscribe<T>(Func<T, Task> handler);
    Task Publish<T>(T @event);
}
