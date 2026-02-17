namespace QueueService.Models;

public class QueueConfiguration
{
    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }
    public int MaxCounters { get; set; }
}
