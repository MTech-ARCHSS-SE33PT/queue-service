using QueueService.Models;

namespace QueueService.Services;

public interface IRedisQueueService
{
    Task RemoveQueueAsync(Guid tenantId, Guid serviceId);
    Task EnqueueAsync(QueueEntry entry);
    Task<Guid?> DequeueNextAsync(Guid tenantId, Guid serviceId);
    Task<long> GetWaitingCountAsync(Guid tenantId, Guid serviceId);
    Task RemoveAsync(Guid tenantId, Guid serviceId, Guid queueEntryId);
}