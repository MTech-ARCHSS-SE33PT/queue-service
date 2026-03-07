using QueueService.Repositories;
using QueueService.Models;
using QueueService.DTOs;
using QueueService.Events;

namespace QueueService.Services;

public class QueueOrchestratorService
{
    private readonly IQueueRepository _repository;
    private readonly IRedisQueueService _redis;
    private readonly IEventPublisher _publisher;

    public QueueOrchestratorService(
        IQueueRepository repository,
        IRedisQueueService redis,
        IEventPublisher publisher)
    {
        _repository = repository;
        _redis = redis;
        _publisher = publisher;
    }

    // ============================
    // CONFIGURE
    // ============================
    public async Task Configure(
        Guid tenantId,
        Guid serviceId,
        string serviceName,
        string locationName,
        int maxCounters)
    {
        await _repository.ConfigureAsync(
            tenantId, serviceId, serviceName, locationName, maxCounters);
    }

    // ============================
    // CREATE TICKET
    // ============================
    public async Task<QueueEntryDto> CreateTicket(
        Guid tenantId,
        Guid serviceId,
        Guid? appointmentId,
        int priority)
    {
        var ticket = await _repository.CreateTicketAsync(
            tenantId, serviceId, appointmentId, priority);

        await _redis.EnqueueAsync(ticket);

        await _publisher.PublishAsync("ticket_created",
            new TicketCreatedEvent(ticket));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(tenantId, serviceId));

        return Map(ticket);
    }

    // ============================
    // CALL NEXT
    // ============================
    public async Task<QueueEntryDto?> CallNext(
        Guid tenantId,
        Guid serviceId,
        string counterId)
    {
        var ticketId = await _redis.DequeueNextAsync(tenantId, serviceId);

        if (ticketId == null)
            return null;

        var ticket = await _repository.MarkAsCalledAsync(
            ticketId.Value, counterId);

        await _publisher.PublishAsync("ticket_called",
            new TicketCalledEvent(ticket));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(tenantId, serviceId));

        return Map(ticket);
    }

    // ============================
    // COMPLETE
    // ============================
    public async Task CompleteTicket(Guid queueEntryId)
    {
        var ticket = await _repository.MarkAsCompletedAsync(queueEntryId);

        await _publisher.PublishAsync("ticket_completed",
            new TicketCompletedEvent(ticket));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(ticket.TenantId, ticket.ServiceId));
    }

    // ============================
    // STATUS
    // ============================
    public async Task<object> GetStatus(
        Guid tenantId,
        Guid serviceId)
    {
        var waiting = await _redis.GetWaitingCountAsync(
            tenantId, serviceId);

        var serving = await _repository.GetServingTicketsAsync(
            tenantId, serviceId);

        return new
        {
            WaitingCount = waiting,
            Serving = serving
        };
    }

    // ============================
    // TENANT VIEW
    // ============================
    public async Task<List<QueueConfigurationDto>> GetQueuesByTenantAsync(Guid tenantId)
    {
        var configs = await _repository.GetQueuesByTenantAsync(tenantId);

        return configs.Select(q => new QueueConfigurationDto
        {
            Id = q.Id,
            ServiceId = q.ServiceId,
            ServiceName = q.ServiceName,
            LocationName = q.LocationName,
            MaxCounters = q.MaxCounters,
            Entries = q.QueueEntries.Select(e => new QueueEntryDto
            {
                Id = e.Id,
                TicketNumber = e.TicketNumber,
                PriorityLevel = e.PriorityLevel,
                Status = e.Status,
                CounterId = e.CounterId,
                EnqueuedAt = e.EnqueuedAt,
                CalledAt = e.CalledAt,
                ServedAt = e.ServedAt
            }).ToList()
        }).ToList();
    }

    private static QueueEntryDto Map(QueueEntry entry)
    {
        return new QueueEntryDto
        {
            Id = entry.Id,
            TicketNumber = entry.TicketNumber,
            PriorityLevel = entry.PriorityLevel,
            Status = entry.Status,
            CounterId = entry.CounterId,
            EnqueuedAt = entry.EnqueuedAt,
            CalledAt = entry.CalledAt,
            ServedAt = entry.ServedAt
        };
    }

    public async Task Update(Guid tenantId, Guid serviceId, int maxCounters)
{
    await _repository.UpdateAsync(tenantId, serviceId, maxCounters);
}

public async Task Delete(Guid tenantId, Guid serviceId)
{
    await _repository.DeleteAsync(tenantId, serviceId);
    await _redis.RemoveQueueAsync(tenantId, serviceId);
}
}