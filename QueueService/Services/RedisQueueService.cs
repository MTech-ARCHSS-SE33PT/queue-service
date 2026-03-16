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

    private static string MilestoneKey(Guid tenantId, Guid serviceId, int positionAhead)
        => $"queue:{tenantId}:{serviceId}:milestone:{positionAhead}:notified";

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

    public async Task<int?> GetPositionAheadAsync(Guid tenantId, Guid serviceId, Guid queueEntryId)
    {
        var rank = await _redis.SortedSetRankAsync(
            QueueKey(tenantId, serviceId),
            queueEntryId.ToString(),
            Order.Descending);

        return rank is null ? null : (int)rank.Value;
    }

    public async Task<Guid?> GetTicketIdAtPositionAheadAsync(Guid tenantId, Guid serviceId, int positionAhead)
    {
        if (positionAhead < 0)
            return null;

        var values = await _redis.SortedSetRangeByRankAsync(
            QueueKey(tenantId, serviceId),
            positionAhead,
            positionAhead,
            Order.Descending);

        if (values.Length == 0 || values[0].IsNullOrEmpty)
            return null;

        return Guid.TryParse(values[0].ToString(), out var id) ? id : null;
    }

    public Task<bool> MarkMilestoneNotifiedAsync(Guid tenantId, Guid serviceId, Guid queueEntryId, int positionAhead)
    {
        if (positionAhead < 0)
            return Task.FromResult(false);

        return _redis.SetAddAsync(
            MilestoneKey(tenantId, serviceId, positionAhead),
            queueEntryId.ToString());
    }

    // ============================
    // COUNTER MANAGEMENT (USER AWARE)
    // ============================
    public async Task<(bool Success, string? Error)> SetCounterAsync(
        Guid tenantId,
        Guid serviceId,
        string userId,
        string counterNumber,
        int maxCounters)
    {
        var counterKey = $"queue:{tenantId}:{serviceId}:counter-map";
        var userKey = $"queue:{tenantId}:{serviceId}:user-map";

        // Validate counter range
        if (!int.TryParse(counterNumber, out var number))
            return (false, "Invalid counter number.");

        if (number < 1 || number > maxCounters)
            return (false, "Counter out of range.");

        // Check if user already has a counter
        var existingCounter = await _redis.HashGetAsync(userKey, userId);
        if (!existingCounter.IsNullOrEmpty)
            return (false, $"User already assigned to counter {existingCounter}.");

        // Check if counter already taken
        var existingUser = await _redis.HashGetAsync(counterKey, counterNumber);
        if (!existingUser.IsNullOrEmpty)
            return (false, "Counter already taken.");

        // Assign both mappings
        await _redis.HashSetAsync(counterKey, counterNumber, userId);
        await _redis.HashSetAsync(userKey, userId, counterNumber);

        return (true, null);
    }

    public async Task RemoveCounterAsync(
        Guid tenantId,
        Guid serviceId,
        string userId)
    {
        var counterKey = $"queue:{tenantId}:{serviceId}:counter-map";
        var userKey = $"queue:{tenantId}:{serviceId}:user-map";

        var counterNumber = await _redis.HashGetAsync(userKey, userId);

        if (!counterNumber.IsNullOrEmpty)
        {
            await _redis.HashDeleteAsync(counterKey, counterNumber);
            await _redis.HashDeleteAsync(userKey, userId);
        }
    }

    public async Task<Dictionary<string, string>> GetActiveCountersAsync(
        Guid tenantId,
        Guid serviceId)
    {
        var counterKey = $"queue:{tenantId}:{serviceId}:counter-map";

        var entries = await _redis.HashGetAllAsync(counterKey);

        return entries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString());
    }
}
