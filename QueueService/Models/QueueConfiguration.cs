using QueueService.Models;
public class QueueConfiguration
{
    public Guid Id { get; set; }  // Add primary key
    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }

    public string ServiceName { get; set; } = default!;
    public string LocationName { get; set; } = default!;
    public int MaxCounters { get; set; }

    public ICollection<QueueEntry> QueueEntries { get; set; } = new List<QueueEntry>();
}