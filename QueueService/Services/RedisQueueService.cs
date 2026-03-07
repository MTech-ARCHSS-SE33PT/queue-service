using StackExchange.Redis;
using QueueService.Infrastructure;
using QueueService.Models;

namespace QueueService.Services;

public class RedisQueueService : IRedisQueueService
{
    private readonly IDatabase _redis;

    public RedisQueueService(RedisConnection redis)
    {
        _redis = redis.Db;
    }

    private string QueueKey(Guid tenantId, Guid serviceId)
        => $"queue:{tenantId}:{serviceId}";

    // ============================
    // ADD TICKET TO REDIS QUEUE
    // ============================
    public async Task EnqueueAsync(QueueEntry entry)
    {
        var score = (entry.PriorityLevel * 1_000_000) +
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        await _redis.SortedSetAddAsync(
            QueueKey(entry.TenantId, entry.ServiceId),
            entry.Id.ToString(),
            score);
    }

    // ============================
    // POP NEXT TICKET
    // ============================
    public async Task<Guid?> DequeueNextAsync(Guid tenantId, Guid serviceId)
    {
        var result = await _redis.SortedSetPopAsync(
            QueueKey(tenantId, serviceId),
            Order.Descending);

        if (result == null || result.Value.Element.IsNull)
            return null;

        return Guid.Parse(result.Value.Element!);
    }

    // ============================
    // COUNT WAITING
    // ============================
    public async Task<long> GetWaitingCountAsync(Guid tenantId, Guid serviceId)
    {
        return await _redis.SortedSetLengthAsync(
            QueueKey(tenantId, serviceId));
    }

    // ============================
    // REMOVE SPECIFIC TICKET
    // ============================
    public async Task RemoveAsync(Guid tenantId, Guid serviceId, Guid queueEntryId)
    {
        await _redis.SortedSetRemoveAsync(
            QueueKey(tenantId, serviceId),
            queueEntryId.ToString());
    }

public async Task RemoveQueueAsync(Guid tenantId, Guid serviceId)
{
    await _redis.KeyDeleteAsync(
        QueueKey(tenantId, serviceId));
}
}