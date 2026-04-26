using QueueService.Models;
using QueueService.Services;

namespace QueueService.Tests;

public sealed class InMemoryRedisQueueServiceTests
{
    [Fact]
    public async Task CounterAssignments_PreventDuplicateUsersAndCounters_ThenAllowReuseAfterRemoval()
    {
        var queue = new InMemoryRedisQueueService();
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var first = await queue.SetCounterAsync(tenantId, serviceId, "staff-1", "1", maxCounters: 2);
        var sameUser = await queue.SetCounterAsync(tenantId, serviceId, "staff-1", "2", maxCounters: 2);
        var sameCounter = await queue.SetCounterAsync(tenantId, serviceId, "staff-2", "1", maxCounters: 2);

        await queue.RemoveCounterAsync(tenantId, serviceId, "staff-1");
        var reused = await queue.SetCounterAsync(tenantId, serviceId, "staff-2", "1", maxCounters: 2);

        Assert.True(first.Success);
        Assert.False(sameUser.Success);
        Assert.Equal("User already assigned to counter 1.", sameUser.Error);
        Assert.False(sameCounter.Success);
        Assert.Equal("Counter already taken.", sameCounter.Error);
        Assert.True(reused.Success);
    }

    [Fact]
    public async Task QueuePositions_ReturnTicketsByPriorityOrder()
    {
        var queue = new InMemoryRedisQueueService();
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var normal = CreateEntry(tenantId, serviceId, priorityLevel: 0);
        var priority = CreateEntry(tenantId, serviceId, priorityLevel: 1);

        await queue.EnqueueAsync(normal);
        await queue.EnqueueAsync(priority);

        var firstTicketId = await queue.GetTicketIdAtPositionAheadAsync(tenantId, serviceId, positionAhead: 0);
        var normalPosition = await queue.GetPositionAheadAsync(tenantId, serviceId, normal.Id);

        Assert.Equal(priority.Id, firstTicketId);
        Assert.Equal(1, normalPosition);
    }

    private static QueueEntry CreateEntry(Guid tenantId, Guid serviceId, int priorityLevel)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = serviceId,
            PriorityLevel = priorityLevel,
            TicketNumber = $"A{priorityLevel}",
            Status = "WAITING"
        };
}
