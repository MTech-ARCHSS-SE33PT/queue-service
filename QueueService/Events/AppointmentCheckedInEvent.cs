namespace QueueService.Events;

// ===============================
// Appointment Checked In
// ===============================
public record AppointmentCheckedInEvent(
    Guid TenantId,
    Guid ServiceId,
    Guid AppointmentId,
    int PriorityLevel
);

// ===============================
// Ticket Created
// ===============================
public record TicketCreatedEvent(
    Guid TicketId,
    Guid TenantId,
    Guid ServiceId,
    Guid? AppointmentId,
    string TicketNumber,
    int PriorityLevel,
    DateTime EnqueuedAt
);

// ===============================
// Ticket Called
// ===============================
public record TicketCalledEvent(
    Guid TicketId,
    string? CounterId,
    DateTime? CalledAt
);

// ===============================
// Ticket Completed
// ===============================
public record TicketCompletedEvent(
    Guid TicketId,
    DateTime? ServedAt
);

// ===============================
// Queue Updated
// ===============================
public record QueueUpdatedEvent(
    Guid TenantId,
    Guid ServiceId
);