using System.Collections.Concurrent;
using QueueService.Models;

namespace QueueService.Services;

public sealed class InMemoryRedisQueueService : IRedisQueueService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, long>> _queues = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _counterMaps = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _userMaps = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, byte>> _milestones = new();

    private static string QueueKey(Guid tenantId, Guid serviceId)
        => $"queue:{tenantId}:{serviceId}";

    private static string CounterKey(Guid tenantId, Guid serviceId)
        => $"queue:{tenantId}:{serviceId}:counter-map";

    private static string UserKey(Guid tenantId, Guid serviceId)
        => $"queue:{tenantId}:{serviceId}:user-map";

    private static string MilestoneKey(Guid tenantId, Guid serviceId, int positionAhead)
        => $"queue:{tenantId}:{serviceId}:milestone:{positionAhead}:notified";

    public Task RemoveQueueAsync(Guid tenantId, Guid serviceId)
    {
        _queues.TryRemove(QueueKey(tenantId, serviceId), out _);
        _counterMaps.TryRemove(CounterKey(tenantId, serviceId), out _);
        _userMaps.TryRemove(UserKey(tenantId, serviceId), out _);
        return Task.CompletedTask;
    }

    public Task EnqueueAsync(QueueEntry entry)
    {
        var score = (entry.PriorityLevel * 1_000_000L) +
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var queue = _queues.GetOrAdd(
            QueueKey(entry.TenantId, entry.ServiceId),
            _ => new ConcurrentDictionary<Guid, long>());

        queue[entry.Id] = score;
        return Task.CompletedTask;
    }

    public Task<Guid?> DequeueNextAsync(Guid tenantId, Guid serviceId)
    {
        var key = QueueKey(tenantId, serviceId);
        if (!_queues.TryGetValue(key, out var queue) || queue.IsEmpty)
            return Task.FromResult<Guid?>(null);

        KeyValuePair<Guid, long>? best = null;
        foreach (var item in queue)
        {
            if (best is null || item.Value > best.Value.Value)
                best = item;
        }

        if (best is null)
            return Task.FromResult<Guid?>(null);

        return Task.FromResult(queue.TryRemove(best.Value.Key, out _)
            ? best.Value.Key
            : (Guid?)null);
    }

    public Task<int?> GetPositionAheadAsync(Guid tenantId, Guid serviceId, Guid queueEntryId)
    {
        var key = QueueKey(tenantId, serviceId);
        if (!_queues.TryGetValue(key, out var queue) || queue.IsEmpty)
            return Task.FromResult<int?>(null);

        if (!queue.TryGetValue(queueEntryId, out var thisScore))
            return Task.FromResult<int?>(null);

        var ahead = 0;
        foreach (var kvp in queue)
        {
            if (kvp.Key == queueEntryId) continue;
            if (kvp.Value > thisScore) ahead++;
        }

        return Task.FromResult<int?>(ahead);
    }

    public Task<Guid?> GetTicketIdAtPositionAheadAsync(Guid tenantId, Guid serviceId, int positionAhead)
    {
        if (positionAhead < 0)
            return Task.FromResult<Guid?>(null);

        var key = QueueKey(tenantId, serviceId);
        if (!_queues.TryGetValue(key, out var queue) || queue.IsEmpty)
            return Task.FromResult<Guid?>(null);

        var ordered = queue
            .OrderByDescending(x => x.Value)
            .Select(x => x.Key)
            .ToList();

        if (positionAhead >= ordered.Count)
            return Task.FromResult<Guid?>(null);

        return Task.FromResult<Guid?>(ordered[positionAhead]);
    }

    public Task<bool> MarkMilestoneNotifiedAsync(Guid tenantId, Guid serviceId, Guid queueEntryId, int positionAhead)
    {
        if (positionAhead < 0)
            return Task.FromResult(false);

        var set = _milestones.GetOrAdd(
            MilestoneKey(tenantId, serviceId, positionAhead),
            _ => new ConcurrentDictionary<Guid, byte>());

        return Task.FromResult(set.TryAdd(queueEntryId, 0));
    }

    public Task<long> GetWaitingCountAsync(Guid tenantId, Guid serviceId)
    {
        var key = QueueKey(tenantId, serviceId);
        if (!_queues.TryGetValue(key, out var queue))
            return Task.FromResult(0L);

        return Task.FromResult((long)queue.Count);
    }

    public Task RemoveAsync(Guid tenantId, Guid serviceId, Guid queueEntryId)
    {
        var key = QueueKey(tenantId, serviceId);
        if (_queues.TryGetValue(key, out var queue))
            queue.TryRemove(queueEntryId, out _);

        return Task.CompletedTask;
    }

    public Task<(bool Success, string? Error)> SetCounterAsync(
        Guid tenantId,
        Guid serviceId,
        string userId,
        string counterNumber,
        int maxCounters)
    {
        if (!int.TryParse(counterNumber, out var number))
            return Task.FromResult((false, (string?)"Invalid counter number."));

        if (number < 1 || number > maxCounters)
            return Task.FromResult((false, (string?)"Counter out of range."));

        var counterKey = CounterKey(tenantId, serviceId);
        var userKey = UserKey(tenantId, serviceId);

        var counterMap = _counterMaps.GetOrAdd(counterKey, _ => new ConcurrentDictionary<string, string>());
        var userMap = _userMaps.GetOrAdd(userKey, _ => new ConcurrentDictionary<string, string>());

        if (userMap.TryGetValue(userId, out var existingCounter))
            return Task.FromResult((false, (string?)$"User already assigned to counter {existingCounter}."));

        if (counterMap.ContainsKey(counterNumber))
            return Task.FromResult((false, (string?)"Counter already taken."));

        if (!counterMap.TryAdd(counterNumber, userId))
            return Task.FromResult((false, (string?)"Counter already taken."));

        if (!userMap.TryAdd(userId, counterNumber))
        {
            counterMap.TryRemove(counterNumber, out _);
            userMap.TryGetValue(userId, out var currentCounter);
            return Task.FromResult((false, (string?)$"User already assigned to counter {currentCounter ?? "?"}."));
        }

        return Task.FromResult((true, (string?)null));
    }

    public Task RemoveCounterAsync(Guid tenantId, Guid serviceId, string userId)
    {
        var counterKey = CounterKey(tenantId, serviceId);
        var userKey = UserKey(tenantId, serviceId);

        if (_userMaps.TryGetValue(userKey, out var userMap) &&
            userMap.TryRemove(userId, out var counterNumber) &&
            _counterMaps.TryGetValue(counterKey, out var counterMap))
        {
            counterMap.TryRemove(counterNumber, out _);
        }

        return Task.CompletedTask;
    }

    public Task<Dictionary<string, string>> GetActiveCountersAsync(Guid tenantId, Guid serviceId)
    {
        var counterKey = CounterKey(tenantId, serviceId);
        if (!_counterMaps.TryGetValue(counterKey, out var counterMap))
            return Task.FromResult(new Dictionary<string, string>());

        return Task.FromResult(counterMap.ToDictionary(x => x.Key, x => x.Value));
    }
}
