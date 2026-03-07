namespace QueueService.DTOs;

public class QueueConfigurationDto
{
    public Guid Id { get; set; }

    public Guid ServiceId { get; set; }

    public string ServiceName { get; set; } = default!;

    public string LocationName { get; set; } = default!;

    public int MaxCounters { get; set; }

    public List<QueueEntryDto> Entries { get; set; } = new();
}