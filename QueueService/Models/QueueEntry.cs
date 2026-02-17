namespace QueueService.Models;

public class QueueEntry
{
    public Guid QueueEntryId { get; set; } = Guid.NewGuid();
    public Guid? AppointmentId { get; set; }

    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }

    public string TicketNumber { get; set; } = default!;
    public int PriorityLevel { get; set; }

    public string Status { get; set; } = "WAITING";

    public string? CounterId { get; set; }

    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CalledAt { get; set; }
    public DateTime? ServedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
