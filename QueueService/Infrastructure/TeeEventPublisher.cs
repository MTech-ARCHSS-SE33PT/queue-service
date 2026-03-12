using Microsoft.Extensions.Logging;
using QueueService.Events;

namespace QueueService.Infrastructure;

public sealed class TeeEventPublisher : IEventPublisher
{
    private readonly IEventPublisher _first;
    private readonly IEventPublisher _second;
    private readonly ILogger<TeeEventPublisher> _log;

    public TeeEventPublisher(
        IEventPublisher first,
        IEventPublisher second,
        ILogger<TeeEventPublisher> log)
    {
        _first = first;
        _second = second;
        _log = log;
    }

    public async Task PublishAsync<T>(string eventType, T payload)
    {
        try
        {
            await _first.PublishAsync(eventType, payload);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "First publisher failed for {EventType}.", eventType);
        }

        try
        {
            await _second.PublishAsync(eventType, payload);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Second publisher failed for {EventType}.", eventType);
        }
    }
}

