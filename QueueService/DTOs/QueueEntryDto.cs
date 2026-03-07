namespace QueueService.DTOs;

public class QueueEntryDto
{
    public Guid Id { get; set; }

    public string TicketNumber { get; set; } = default!;

    public int PriorityLevel { get; set; }

    public string Status { get; set; } = default!;

    public string? CounterId { get; set; }

    public DateTime EnqueuedAt { get; set; }

    public DateTime? CalledAt { get; set; }

    public DateTime? ServedAt { get; set; }
}