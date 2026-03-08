namespace QueueService.DTOs;

public class StaffTicketDto
{
    public Guid Id { get; set; }

    public string TicketNumber { get; set; } = default!;

    public Guid ServiceId { get; set; }

    public string ServiceName { get; set; } = default!;

    public string Status { get; set; } = default!;

    public int PriorityLevel { get; set; }

    public string? CounterId { get; set; }

    public DateTime EnqueuedAt { get; set; }
}