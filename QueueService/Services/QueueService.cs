using QueueService.Models;
using QueueService.Infrastructure;
using QueueService.Events;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace QueueService.Services;

public class QueueService : IQueueService
{
    private readonly IDatabase _redis;
    private readonly IEventPublisher _publisher;

    private static ConcurrentDictionary<string, QueueConfiguration> _configs = new();
    private static ConcurrentDictionary<Guid, QueueEntry> _tickets = new();

    public QueueService(RedisConnection redis, IEventPublisher publisher)
    {
        _redis = redis.Db;
        _publisher = publisher;
    }

    private string Key(Guid tenantId, Guid serviceId)
        => $"queue:{tenantId}:{serviceId}";

    public Task Configure(Guid tenantId, Guid serviceId, int maxCounters)
    {
        _configs[$"{tenantId}:{serviceId}"] =
            new QueueConfiguration { TenantId = tenantId, ServiceId = serviceId, MaxCounters = maxCounters };

        return Task.CompletedTask;
    }

    public async Task<QueueEntry> CreateTicket(Guid tenantId, Guid serviceId, Guid? appointmentId, int priority)
    {
        var entry = new QueueEntry
        {
            TenantId = tenantId,
            ServiceId = serviceId,
            AppointmentId = appointmentId,
            PriorityLevel = priority,
            TicketNumber = $"T-{DateTime.UtcNow.Ticks % 10000}"
        };

        _tickets[entry.QueueEntryId] = entry;

        var score = (priority * 1_000_000) + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _redis.SortedSetAddAsync(Key(tenantId, serviceId),
            entry.QueueEntryId.ToString(), score);

        await _publisher.PublishAsync("ticket_created", new TicketCreatedEvent(entry));
        await _publisher.PublishAsync("queue_updated", new QueueUpdatedEvent(tenantId, serviceId));

        return entry;
    }

    public async Task<QueueEntry?> CallNext(Guid tenantId, Guid serviceId, string counterId)
    {
        var result = await _redis.SortedSetPopAsync(
            Key(tenantId, serviceId),
            Order.Descending);

        if (result == null || result.Value.Element.IsNull)
            return null;

        var id = Guid.Parse(result.Value.Element!);

        var ticket = _tickets[id];

        ticket.Status = "CALLED";
        ticket.CounterId = counterId;
        ticket.CalledAt = DateTime.UtcNow;

        await _publisher.PublishAsync("ticket_called", new TicketCalledEvent(ticket));
        await _publisher.PublishAsync("queue_updated", new QueueUpdatedEvent(tenantId, serviceId));

        return ticket;
    }

    public async Task CompleteTicket(Guid ticketId)
    {
        if (_tickets.TryGetValue(ticketId, out var ticket))
        {
            ticket.Status = "SERVED";
            ticket.ServedAt = DateTime.UtcNow;

            await _publisher.PublishAsync("ticket_completed", new TicketCompletedEvent(ticket));
            await _publisher.PublishAsync("queue_updated",
                new QueueUpdatedEvent(ticket.TenantId, ticket.ServiceId));
        }
    }

    public async Task<object> GetStatus(Guid tenantId, Guid serviceId)
    {
        var waiting = await _redis.SortedSetLengthAsync(Key(tenantId, serviceId));
        var serving = _tickets.Values
            .Where(x => x.TenantId == tenantId &&
                        x.ServiceId == serviceId &&
                        x.Status == "CALLED");

        return new { WaitingCount = waiting, Serving = serving };
    }
}
