namespace QueueService.Models;

public record AppointmentCheckedInEvent(
    Guid TenantId,
    Guid ServiceId,
    Guid AppointmentId,
    int PriorityLevel);

public record TicketCreatedEvent(QueueEntry Ticket);
public record TicketCalledEvent(QueueEntry Ticket);
public record TicketCompletedEvent(QueueEntry Ticket);
public record QueueUpdatedEvent(Guid TenantId, Guid ServiceId);
