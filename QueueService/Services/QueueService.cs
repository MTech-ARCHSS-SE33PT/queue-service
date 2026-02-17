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

    // Queue configuration per tenant+service
    private static ConcurrentDictionary<string, QueueConfiguration> _configs = new();

    // In-memory ticket store (simulate persistence for now)
    private static ConcurrentDictionary<Guid, QueueEntry> _tickets = new();

    public QueueService(RedisConnection redis, IEventPublisher publisher)
    {
        _redis = redis.Db;
        _publisher = publisher;
    }

    private string QueueKey(Guid tenantId, Guid serviceId)
        => $"queue:{tenantId}:{serviceId}";

    private string ConfigKey(Guid tenantId, Guid serviceId)
        => $"{tenantId}:{serviceId}";

    // ---------------------------
    // CONFIGURE QUEUE
    // ---------------------------
    public Task Configure(Guid tenantId, Guid serviceId, int maxCounters)
    {
        _configs[ConfigKey(tenantId, serviceId)] =
            new QueueConfiguration
            {
                TenantId = tenantId,
                ServiceId = serviceId,
                MaxCounters = maxCounters
            };

        return Task.CompletedTask;
    }

    // ---------------------------
    // CREATE TICKET (Walk-in or Appointment)
    // ---------------------------
    public async Task<QueueEntry> CreateTicket(
        Guid tenantId,
        Guid serviceId,
        Guid? appointmentId,
        int priority)
    {
        var entry = new QueueEntry
        {
            TenantId = tenantId,
            ServiceId = serviceId,
            AppointmentId = appointmentId,
            PriorityLevel = priority,
            TicketNumber = $"T-{DateTime.UtcNow.Ticks % 10000}",
            Status = "WAITING"
        };

        _tickets[entry.QueueEntryId] = entry;

        var score = (priority * 1_000_000) +
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _redis.SortedSetAddAsync(
            QueueKey(tenantId, serviceId),
            entry.QueueEntryId.ToString(),
            score);

        await _publisher.PublishAsync("ticket_created",
            new TicketCreatedEvent(entry));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(tenantId, serviceId));

        return entry;
    }

    // CALL NEXT (Counter-based dequeue)
    // ---------------------------
    public async Task<QueueEntry?> CallNext(
        Guid tenantId,
        Guid serviceId,
        string counterId)
    {
        var configKey = ConfigKey(tenantId, serviceId);

        if (!_configs.TryGetValue(configKey, out var config))
            throw new Exception("Queue not configured.");

        // Count active serving tickets
        var activeServing = _tickets.Values.Count(x =>
            x.TenantId == tenantId &&
            x.ServiceId == serviceId &&
            x.Status == "CALLED");

        if (activeServing >= config.MaxCounters)
            throw new Exception("All counters are busy.");

        // Atomic pop from Redis (highest priority first)
        var result = await _redis.SortedSetPopAsync(
            QueueKey(tenantId, serviceId),
            Order.Descending);

        if (result == null || result.Value.Element.IsNull)
            return null;

        var ticketId = Guid.Parse(result.Value.Element!);

        if (!_tickets.TryGetValue(ticketId, out var ticket))
            return null;

        ticket.Status = "CALLED";
        ticket.CounterId = counterId;
        ticket.CalledAt = DateTime.UtcNow;

        await _publisher.PublishAsync("ticket_called",
            new TicketCalledEvent(ticket));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(tenantId, serviceId));

        return ticket;
    }

    // ---------------------------
    // COMPLETE TICKET
    // ---------------------------
    public async Task CompleteTicket(Guid queueEntryId)
    {
        if (!_tickets.TryGetValue(queueEntryId, out var ticket))
        throw new Exception("Queue entry not found.");


        ticket.Status = "SERVED";
        ticket.ServedAt = DateTime.UtcNow;

        await _publisher.PublishAsync("ticket_completed",
            new TicketCompletedEvent(ticket));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(ticket.TenantId, ticket.ServiceId));
    }

    // ---------------------------
    // GET QUEUE STATUS (Dashboard)
    // ---------------------------
    public async Task<object> GetStatus(Guid tenantId, Guid serviceId)
    {
        var waitingCount = await _redis.SortedSetLengthAsync(
            QueueKey(tenantId, serviceId));

        var servingTickets = _tickets.Values
            .Where(x =>
                x.TenantId == tenantId &&
                x.ServiceId == serviceId &&
                x.Status == "CALLED")
            .ToList();

        return new
        {
            WaitingCount = waitingCount,
            Serving = servingTickets
        };
    }
}
