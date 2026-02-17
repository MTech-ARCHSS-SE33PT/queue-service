using System.Collections.Concurrent;

namespace QueueService.Events;

public class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();

    public void Subscribe<T>(Func<T, Task> handler)
    {
        var handlers = _handlers.GetOrAdd(typeof(T), _ => new());
        handlers.Add(e => handler((T)e));
    }

    public async Task Publish<T>(T @event)
    {
        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            foreach (var handler in handlers)
                await handler(@event);
        }
    }
}
