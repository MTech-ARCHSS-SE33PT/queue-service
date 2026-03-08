using QueueService.Models;

namespace QueueService.Services;

public interface IRedisQueueService
{
    Task RemoveQueueAsync(Guid tenantId, Guid serviceId);
    Task EnqueueAsync(QueueEntry entry);
    Task<Guid?> DequeueNextAsync(Guid tenantId, Guid serviceId);
    Task<long> GetWaitingCountAsync(Guid tenantId, Guid serviceId);
    Task RemoveAsync(Guid tenantId, Guid serviceId, Guid queueEntryId);

// ============================
// COUNTER MANAGEMENT
// ============================

Task<(bool Success, string? Error)> SetCounterAsync(
    Guid tenantId,
    Guid serviceId,
    string userId,
    string counterNumber,
    int maxCounters);

Task RemoveCounterAsync(
    Guid tenantId,
    Guid serviceId,
    string userId);

Task<Dictionary<string, string>> GetActiveCountersAsync(
    Guid tenantId,
    Guid serviceId);
}