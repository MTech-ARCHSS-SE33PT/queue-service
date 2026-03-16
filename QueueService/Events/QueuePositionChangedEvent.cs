namespace QueueService.Events;

public record QueuePositionChangedEnvelope(
    Guid EventId,
    string EventType,
    Guid TenantId,
    DateTime OccurredAt,
    QueuePositionChangedData Data);

public record QueuePositionChangedData(
    string TicketNo,
    int Position,
    QueueCustomer Customer);

public record QueueCustomer(
    string? Name,
    string? Phone);

