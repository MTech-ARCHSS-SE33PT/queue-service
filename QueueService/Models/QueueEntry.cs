namespace QueueService.Models;

public class QueueEntry
{
    // Primary Key (EF Core)
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key to QueueConfiguration
    public Guid QueueConfigurationId { get; set; }
    public QueueConfiguration QueueConfiguration { get; set; } = default!;

    // Multi-tenant routing
    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }

    // Ticket details
    public string TicketNumber { get; set; } = default!;
    public int PriorityLevel { get; set; }

    // Status: WAITING, CALLED, SERVED
    public string Status { get; set; } = "WAITING";

    public Guid? AppointmentId { get; set; }

    public string? CounterId { get; set; }

    // Lifecycle timestamps
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}