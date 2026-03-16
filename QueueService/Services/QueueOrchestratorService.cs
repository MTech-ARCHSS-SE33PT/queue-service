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
            new TicketCreatedEvent(
                ticket.Id,
                ticket.TenantId,
                ticket.ServiceId,
                ticket.AppointmentId,
                ticket.TicketNumber,
                ticket.PriorityLevel,
                ticket.EnqueuedAt
            ));

        await _publisher.PublishAsync("queue_updated",
            new QueueUpdatedEvent(tenantId, serviceId));

        await PublishPositionMilestoneIfNeededAsync(ticket, positionAhead: 3);

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
    // 🚨 Check if this counter already has active ticket
    var servingTickets = await _repository.GetServingTicketsAsync(
        tenantId, serviceId);

    var alreadyServing = servingTickets
        .FirstOrDefault(t => t.CounterId == counterId);

    if (alreadyServing != null)
    {
        throw new InvalidOperationException(
            "This counter is already serving a ticket.");
    }

    var ticketId = await _redis.DequeueNextAsync(tenantId, serviceId);

    if (ticketId == null)
    {
        var nextFromDb = await _repository
            .GetNextWaitingTicketAsync(tenantId, serviceId);

        if (nextFromDb == null)
            return null;

        ticketId = nextFromDb.Id;
    }

    var ticket = await _repository
        .MarkAsCalledAsync(ticketId.Value, counterId);

    await _publisher.PublishAsync("ticket_called",
        new TicketCalledEvent(
            ticket.Id,
            ticket.CounterId,
            ticket.CalledAt
        ));

    await _publisher.PublishAsync("queue_updated",
        new QueueUpdatedEvent(tenantId, serviceId));

    await PublishPositionMilestoneIfNeededAsync(tenantId, serviceId, positionAhead: 3);

    return Map(ticket);
}
    // ============================
    // COMPLETE
    // ============================
    public async Task CompleteTicket(Guid queueEntryId)
    {
        var ticket = await _repository.MarkAsCompletedAsync(queueEntryId);

        await _publisher.PublishAsync("ticket_completed",
            new TicketCompletedEvent(
                ticket.Id,
                ticket.ServedAt
            ));

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
    // TENANT VIEW (CONFIGURATIONS)
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
            Entries = q.QueueEntries.Select(Map).ToList()
        }).ToList();
    }

    // ============================
    // ✅ STAFF TICKETS (CLEAN VERSION)
    // ============================
    public async Task<List<StaffTicketDto>> GetStaffTicketsAsync(
        Guid tenantId,
        Guid serviceId)
    {
        return await _repository.GetStaffTicketsAsync(tenantId, serviceId);
    }

    // ============================
    // BASIC TICKET VIEW
    // ============================
    public async Task<List<QueueEntryDto>> GetTicketsByTenantAsync(Guid tenantId)
    {
        var tickets = await _repository.GetTicketsByTenantAsync(tenantId);
        return tickets.Select(Map).ToList();
    }

    public async Task<int?> GetMaxCountersAsync(Guid tenantId, Guid serviceId)
    {
        return await _repository.GetMaxCountersAsync(tenantId, serviceId);
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

    // ============================
    // COUNTER MANAGEMENT
    // ============================
    public async Task<(bool Success, string? Error)> SetCounterAsync(
        Guid tenantId,
        Guid serviceId,
        string userId,
        string counterNumber)
    {
        var maxCounters = await _repository.GetMaxCountersAsync(tenantId, serviceId);

        if (maxCounters == null)
            return (false, "Queue configuration not found.");

        return await _redis.SetCounterAsync(
            tenantId,
            serviceId,
            userId,
            counterNumber,
            maxCounters.Value);
    }

    public async Task RemoveCounterAsync(
        Guid tenantId,
        Guid serviceId,
        string userId)
    {
        await _redis.RemoveCounterAsync(tenantId, serviceId, userId);
    }

    public async Task<Dictionary<string, string>> GetActiveCountersAsync(
        Guid tenantId,
        Guid serviceId)
    {
        return await _redis.GetActiveCountersAsync(tenantId, serviceId);
    }

    // ============================
    // MAPPER
    // ============================
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

    private async Task PublishPositionMilestoneIfNeededAsync(QueueEntry ticket, int positionAhead)
    {
        var ahead = await _redis.GetPositionAheadAsync(ticket.TenantId, ticket.ServiceId, ticket.Id);
        if (ahead is null || ahead.Value != positionAhead)
            return;

        var firstTime = await _redis.MarkMilestoneNotifiedAsync(ticket.TenantId, ticket.ServiceId, ticket.Id, positionAhead);
        if (!firstTime)
            return;

        await _publisher.PublishAsync(
            "QueuePositionChanged",
            new QueuePositionChangedEnvelope(
                EventId: Guid.NewGuid(),
                EventType: "QueuePositionChanged",
                TenantId: ticket.TenantId,
                OccurredAt: DateTime.UtcNow,
                Data: new QueuePositionChangedData(
                    TicketNo: ticket.TicketNumber,
                    Position: positionAhead,
                    Customer: new QueueCustomer(null, null))));
    }

    private async Task PublishPositionMilestoneIfNeededAsync(Guid tenantId, Guid serviceId, int positionAhead)
    {
        var ticketId = await _redis.GetTicketIdAtPositionAheadAsync(tenantId, serviceId, positionAhead);
        if (ticketId is null)
            return;

        var firstTime = await _redis.MarkMilestoneNotifiedAsync(tenantId, serviceId, ticketId.Value, positionAhead);
        if (!firstTime)
            return;

        var ticket = await _repository.GetTicketByIdAsync(ticketId.Value);
        if (ticket is null)
            return;

        await _publisher.PublishAsync(
            "QueuePositionChanged",
            new QueuePositionChangedEnvelope(
                EventId: Guid.NewGuid(),
                EventType: "QueuePositionChanged",
                TenantId: tenantId,
                OccurredAt: DateTime.UtcNow,
                Data: new QueuePositionChangedData(
                    TicketNo: ticket.TicketNumber,
                    Position: positionAhead,
                    Customer: new QueueCustomer(null, null))));
    }
}
