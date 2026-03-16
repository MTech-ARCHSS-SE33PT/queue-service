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
    // POSITION / MILESTONES
    // ============================
    /// <summary>
    /// Returns how many waiting tickets are ahead of this ticket (0 means "next").
    /// </summary>
    Task<int?> GetPositionAheadAsync(Guid tenantId, Guid serviceId, Guid queueEntryId);

    /// <summary>
    /// Returns the ticketId that currently has <paramref name="positionAhead"/> tickets ahead (0 means "next").
    /// </summary>
    Task<Guid?> GetTicketIdAtPositionAheadAsync(Guid tenantId, Guid serviceId, int positionAhead);

    /// <summary>
    /// Marks this ticket as having triggered the milestone. Returns true only the first time.
    /// </summary>
    Task<bool> MarkMilestoneNotifiedAsync(Guid tenantId, Guid serviceId, Guid queueEntryId, int positionAhead);

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
