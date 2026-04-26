using Microsoft.AspNetCore.Mvc;
using Moq;
using QueueService.Controllers;
using QueueService.DTOs;
using QueueService.Events;
using QueueService.Infrastructure.ServiceBus;
using QueueService.Models;
using QueueService.Repositories;
using QueueService.Services;

namespace QueueService.Tests;

public sealed class DebugAndQueueBranchTests
{
    [Fact]
    public void DebugInbound_ReturnsSnapshotItems()
    {
        var store = new InboundMessageStore();
        store.Record("ignored", "m-1", DateTimeOffset.UtcNow);
        store.RecordAppointmentCheckedIn("appointment_checked_in", "m-2", DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var controller = new DebugInboundController();

        var result = controller.Get(store);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(2, store.Snapshot().Count);
    }

    [Fact]
    public void InboundMessageStore_TrimsOldItems()
    {
        var store = new InboundMessageStore();

        for (var i = 0; i < 55; i++)
            store.Record("subject", $"m-{i}", DateTimeOffset.UtcNow);

        var snapshot = store.Snapshot();
        Assert.Equal(50, snapshot.Count);
        Assert.DoesNotContain(snapshot, item => item.MessageId == "m-0");
    }

    [Fact]
    public async Task DebugPublish_UsesProvidedAndGeneratedPayloads()
    {
        var publisher = new CapturingPublisher();
        var controller = new DebugPublishController(publisher);
        var created = new TicketCreatedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "T0001", 1, DateTime.UtcNow);
        var called = new TicketCalledEvent(Guid.NewGuid(), "C1", DateTime.UtcNow);
        var positionChanged = new QueuePositionChangedEnvelope(
            Guid.NewGuid(),
            "QueuePositionChanged",
            Guid.NewGuid(),
            DateTime.UtcNow,
            new QueuePositionChangedData("Q-001", 3, new QueueCustomer("Ana", "+6512345678")));

        Assert.IsType<OkObjectResult>(await controller.PublishTicketCreated(created));
        Assert.IsType<OkObjectResult>(await controller.PublishTicketCreated(null));
        Assert.IsType<OkObjectResult>(await controller.PublishTicketCalled(called));
        Assert.IsType<OkObjectResult>(await controller.PublishTicketCalled(null));
        Assert.IsType<OkObjectResult>(await controller.PublishQueuePositionChanged(positionChanged));
        Assert.IsType<OkObjectResult>(await controller.PublishQueuePositionChanged(null));

        Assert.Equal(
            new[] { "ticket_created", "ticket_created", "ticket_called", "ticket_called", "QueuePositionChanged", "QueuePositionChanged" },
            publisher.EventTypes);
    }

    [Fact]
    public async Task QueueController_ReturnsRemainingReadAndCounterBranches()
    {
        var repository = new Mock<IQueueRepository>();
        var redis = new Mock<IRedisQueueService>();
        var publisher = new Mock<IEventPublisher>();
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var configs = new List<QueueConfiguration>
        {
            new()
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ServiceId = serviceId,
                ServiceName = "Service",
                LocationName = "Desk",
                MaxCounters = 2,
                QueueEntries = new List<QueueEntry> { Entry(tenantId, serviceId, "A001") }
            }
        };
        var tickets = new List<QueueEntry> { Entry(tenantId, serviceId, "A002") };
        var staffTickets = new List<StaffTicketDto> { new() { TicketNumber = "A003", CounterId = "C1" } };

        repository.Setup(r => r.GetQueuesByTenantAsync(tenantId)).ReturnsAsync(configs);
        repository.Setup(r => r.GetTicketsByTenantAsync(tenantId)).ReturnsAsync(tickets);
        repository.SetupSequence(r => r.GetMaxCountersAsync(tenantId, serviceId))
            .ReturnsAsync((int?)null)
            .ReturnsAsync(2)
            .ReturnsAsync(2)
            .ReturnsAsync(2);
        repository.Setup(r => r.GetStaffTicketsAsync(tenantId, serviceId)).ReturnsAsync(staffTickets);
        redis.SetupSequence(r => r.SetCounterAsync(tenantId, serviceId, "staff-1", "1", 2))
            .ReturnsAsync((false, "Counter already taken."))
            .ReturnsAsync((true, (string?)null));
        redis.Setup(r => r.GetActiveCountersAsync(tenantId, serviceId))
            .ReturnsAsync(new Dictionary<string, string> { ["1"] = "staff-1" });

        var controller = new QueueController(new QueueOrchestratorService(repository.Object, redis.Object, publisher.Object));

        Assert.IsType<OkObjectResult>(await controller.GetQueuesByTenant(tenantId));
        Assert.IsType<OkObjectResult>(await controller.GetTicketsByTenant(tenantId));
        Assert.IsType<NotFoundObjectResult>(await controller.GetMaxCounters(tenantId, serviceId));
        Assert.IsType<OkObjectResult>(await controller.GetMaxCounters(tenantId, serviceId));
        Assert.IsType<BadRequestObjectResult>(await controller.SetCounter(tenantId, serviceId, "staff-1", "1"));
        Assert.IsType<OkObjectResult>(await controller.SetCounter(tenantId, serviceId, "staff-1", "1"));
        Assert.IsType<OkObjectResult>(await controller.GetActiveCounters(tenantId, serviceId));
        Assert.IsType<OkResult>(await controller.ReleaseCounter(tenantId, serviceId, "staff-1"));
        Assert.IsType<OkObjectResult>(await controller.GetStaffTickets(tenantId, serviceId));
    }

    [Fact]
    public async Task InMemoryRedisQueueService_CoversEmptyRemovalAndInvalidCounterBranches()
    {
        var queue = new InMemoryRedisQueueService();
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var entry = Entry(tenantId, serviceId, "A004");

        Assert.Null(await queue.DequeueNextAsync(tenantId, serviceId));
        Assert.Null(await queue.GetPositionAheadAsync(tenantId, serviceId, entry.Id));
        Assert.Null(await queue.GetTicketIdAtPositionAheadAsync(tenantId, serviceId, -1));
        Assert.False(await queue.MarkMilestoneNotifiedAsync(tenantId, serviceId, entry.Id, -1));
        Assert.Empty(await queue.GetActiveCountersAsync(tenantId, serviceId));

        var invalidCounter = await queue.SetCounterAsync(tenantId, serviceId, "staff-1", "abc", 2);
        var outOfRange = await queue.SetCounterAsync(tenantId, serviceId, "staff-1", "3", 2);
        await queue.RemoveAsync(tenantId, serviceId, entry.Id);
        await queue.RemoveCounterAsync(tenantId, serviceId, "missing");
        await queue.RemoveQueueAsync(tenantId, serviceId);

        Assert.False(invalidCounter.Success);
        Assert.Equal("Invalid counter number.", invalidCounter.Error);
        Assert.False(outOfRange.Success);
        Assert.Equal("Counter out of range.", outOfRange.Error);
    }

    private static QueueEntry Entry(Guid tenantId, Guid serviceId, string ticketNumber)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = serviceId,
            TicketNumber = ticketNumber,
            PriorityLevel = 1,
            Status = "WAITING",
            EnqueuedAt = DateTime.UtcNow
        };

    private sealed class CapturingPublisher : IEventPublisher
    {
        private readonly List<string> _eventTypes = new();

        public IReadOnlyList<string> EventTypes => _eventTypes;

        public Task PublishAsync<T>(string eventType, T payload)
        {
            _eventTypes.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
