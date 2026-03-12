using Xunit;
using Moq;
using QueueService.Services;
using QueueService.Repositories;
using QueueService.Events;
using QueueService.Models;
using QueueService.DTOs;

namespace QueueService.Tests;

public class QueueOrchestratorServiceTests
{
    private readonly Mock<IQueueRepository> _mockRepository;
    private readonly Mock<IRedisQueueService> _mockRedis;
    private readonly Mock<IEventPublisher> _mockPublisher;
    private readonly QueueOrchestratorService _service;

    public QueueOrchestratorServiceTests()
    {
        _mockRepository = new Mock<IQueueRepository>();
        _mockRedis = new Mock<IRedisQueueService>();
        _mockPublisher = new Mock<IEventPublisher>();
        _service = new QueueOrchestratorService(
            _mockRepository.Object,
            _mockRedis.Object,
            _mockPublisher.Object);
    }

    [Fact]
    public async Task Configure_ShouldCallRepositoryConfigure()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var serviceName = "Test Service";
        var locationName = "Test Location";
        var maxCounters = 5;

        // Act
        await _service.Configure(tenantId, serviceId, serviceName, locationName, maxCounters);

        // Assert
        _mockRepository.Verify(r => r.ConfigureAsync(
            tenantId, serviceId, serviceName, locationName, maxCounters), Times.Once);
    }

    [Fact]
    public async Task CreateTicket_ShouldCreateTicketAndEnqueueAndPublishEvents()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var priority = 1;
        var ticket = new QueueEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = serviceId,
            AppointmentId = appointmentId,
            TicketNumber = "A001",
            PriorityLevel = priority,
            EnqueuedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.CreateTicketAsync(
            tenantId, serviceId, appointmentId, priority)).ReturnsAsync(ticket);

        // Act
        var result = await _service.CreateTicket(tenantId, serviceId, appointmentId, priority);

        // Assert
        Assert.Equal(ticket.Id, result.Id);
        Assert.Equal(ticket.TicketNumber, result.TicketNumber);
        _mockRepository.Verify(r => r.CreateTicketAsync(
            tenantId, serviceId, appointmentId, priority), Times.Once);
        _mockRedis.Verify(r => r.EnqueueAsync(ticket), Times.Once);
        _mockPublisher.Verify(p => p.PublishAsync("ticket_created", It.IsAny<TicketCreatedEvent>()), Times.Once);
        _mockPublisher.Verify(p => p.PublishAsync("queue_updated", It.IsAny<QueueUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task CallNext_ShouldReturnNullWhenNoTickets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>());
        _mockRedis.Setup(r => r.DequeueNextAsync(tenantId, serviceId))
            .ReturnsAsync((Guid?)null);
        _mockRepository.Setup(r => r.GetNextWaitingTicketAsync(tenantId, serviceId))
            .ReturnsAsync((QueueEntry?)null);

        // Act
        var result = await _service.CallNext(tenantId, serviceId, counterId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task CallNext_ShouldThrowWhenCounterAlreadyServing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";
        var servingTicket = new QueueEntry { CounterId = counterId };

        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry> { servingTicket });

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CallNext(tenantId, serviceId, counterId));
        Assert.Contains("already serving", exception.Message);
    }

    [Fact]
    public async Task CompleteTicket_ShouldMarkAsCompletedAndPublishEvents()
    {
        // Arrange
        var queueEntryId = Guid.NewGuid();
        var ticket = new QueueEntry
        {
            Id = queueEntryId,
            TenantId = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            ServedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.MarkAsCompletedAsync(queueEntryId))
            .ReturnsAsync(ticket);

        // Act
        await _service.CompleteTicket(queueEntryId);

        // Assert
        _mockRepository.Verify(r => r.MarkAsCompletedAsync(queueEntryId), Times.Once);
        _mockPublisher.Verify(p => p.PublishAsync("ticket_completed", It.IsAny<TicketCompletedEvent>()), Times.Once);
        _mockPublisher.Verify(p => p.PublishAsync("queue_updated", It.IsAny<QueueUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task GetStatus_ShouldReturnWaitingCountAndServing()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var waitingCount = 5L;
        var servingTickets = new List<QueueEntry>
        {
            new QueueEntry { Id = Guid.NewGuid(), CounterId = "Counter1" }
        };

        _mockRedis.Setup(r => r.GetWaitingCountAsync(tenantId, serviceId))
            .ReturnsAsync(waitingCount);
        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(servingTickets);

        // Act
        var result = await _service.GetStatus(tenantId, serviceId) as dynamic;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(waitingCount, (long)result.WaitingCount);
        Assert.Equal(servingTickets, (List<QueueEntry>)result.Serving);
    }

    [Fact]
    public async Task GetQueuesByTenantAsync_ShouldReturnMappedConfigurations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var config = new QueueConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = Guid.NewGuid(),
            ServiceName = "Test Service",
            LocationName = "Test Location",
            MaxCounters = 5,
            QueueEntries = new List<QueueEntry>
            {
                new QueueEntry { Id = Guid.NewGuid(), TicketNumber = "A001", PriorityLevel = 1, Status = "WAITING" }
            }
        };

        _mockRepository.Setup(r => r.GetQueuesByTenantAsync(tenantId))
            .ReturnsAsync(new List<QueueConfiguration> { config });

        // Act
        var result = await _service.GetQueuesByTenantAsync(tenantId);

        // Assert
        Assert.Single(result);
        Assert.Equal(config.Id, result[0].Id);
        Assert.Equal(config.ServiceName, result[0].ServiceName);
        Assert.Single(result[0].Entries);
        _mockRepository.Verify(r => r.GetQueuesByTenantAsync(tenantId), Times.Once);
    }

    [Fact]
    public async Task GetStaffTicketsAsync_ShouldReturnFromRepository()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var staffTickets = new List<StaffTicketDto>
        {
            new StaffTicketDto { Id = Guid.NewGuid(), TicketNumber = "A001", ServiceName = "Test Service" }
        };

        _mockRepository.Setup(r => r.GetStaffTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(staffTickets);

        // Act
        var result = await _service.GetStaffTicketsAsync(tenantId, serviceId);

        // Assert
        Assert.Equal(staffTickets, result);
        _mockRepository.Verify(r => r.GetStaffTicketsAsync(tenantId, serviceId), Times.Once);
    }

    [Fact]
    public async Task GetTicketsByTenantAsync_ShouldReturnMappedTickets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tickets = new List<QueueEntry>
        {
            new QueueEntry
            {
                Id = Guid.NewGuid(),
                TicketNumber = "A001",
                PriorityLevel = 1,
                Status = "WAITING",
                CounterId = null,
                EnqueuedAt = DateTime.UtcNow,
                CalledAt = null,
                ServedAt = null
            }
        };

        _mockRepository.Setup(r => r.GetTicketsByTenantAsync(tenantId))
            .ReturnsAsync(tickets);

        // Act
        var result = await _service.GetTicketsByTenantAsync(tenantId);

        // Assert
        Assert.Single(result);
        Assert.Equal(tickets[0].Id, result[0].Id);
        Assert.Equal(tickets[0].TicketNumber, result[0].TicketNumber);
        _mockRepository.Verify(r => r.GetTicketsByTenantAsync(tenantId), Times.Once);
    }

    [Fact]
    public async Task GetMaxCountersAsync_ShouldReturnFromRepository()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 5;

        _mockRepository.Setup(r => r.GetMaxCountersAsync(tenantId, serviceId))
            .ReturnsAsync(maxCounters);

        // Act
        var result = await _service.GetMaxCountersAsync(tenantId, serviceId);

        // Assert
        Assert.Equal(maxCounters, result);
        _mockRepository.Verify(r => r.GetMaxCountersAsync(tenantId, serviceId), Times.Once);
    }

    [Fact]
    public async Task Update_ShouldCallRepositoryUpdate()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 10;

        // Act
        await _service.Update(tenantId, serviceId, maxCounters);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(tenantId, serviceId, maxCounters), Times.Once);
    }

    [Fact]
    public async Task Delete_ShouldCallRepositoryAndRedisDelete()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        // Act
        await _service.Delete(tenantId, serviceId);

        // Assert
        _mockRepository.Verify(r => r.DeleteAsync(tenantId, serviceId), Times.Once);
        _mockRedis.Verify(r => r.RemoveQueueAsync(tenantId, serviceId), Times.Once);
    }

    [Fact]
    public async Task SetCounterAsync_ShouldReturnSuccessWhenValid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var userId = "user1";
        var counterNumber = "1";
        var maxCounters = 5;

        _mockRepository.Setup(r => r.GetMaxCountersAsync(tenantId, serviceId))
            .ReturnsAsync(maxCounters);
        _mockRedis.Setup(r => r.SetCounterAsync(tenantId, serviceId, userId, counterNumber, maxCounters))
            .ReturnsAsync((true, (string?)null));

        // Act
        var result = await _service.SetCounterAsync(tenantId, serviceId, userId, counterNumber);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        _mockRepository.Verify(r => r.GetMaxCountersAsync(tenantId, serviceId), Times.Once);
        _mockRedis.Verify(r => r.SetCounterAsync(tenantId, serviceId, userId, counterNumber, maxCounters), Times.Once);
    }

    [Fact]
    public async Task SetCounterAsync_ShouldReturnErrorWhenQueueNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var userId = "user1";
        var counterNumber = "1";

        _mockRepository.Setup(r => r.GetMaxCountersAsync(tenantId, serviceId))
            .ReturnsAsync((int?)null);

        // Act
        var result = await _service.SetCounterAsync(tenantId, serviceId, userId, counterNumber);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Queue configuration not found.", result.Error);
    }

    [Fact]
    public async Task RemoveCounterAsync_ShouldCallRedisRemove()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var userId = "user1";

        // Act
        await _service.RemoveCounterAsync(tenantId, serviceId, userId);

        // Assert
        _mockRedis.Verify(r => r.RemoveCounterAsync(tenantId, serviceId, userId), Times.Once);
    }

    [Fact]
    public async Task ReleaseCounterIfIdleAsync_ShouldReturnNotAssigned_WhenUserHasNoCounter()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var userId = "user1";

        _mockRedis.Setup(r => r.GetUserCounterAsync(tenantId, serviceId, userId))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.ReleaseCounterIfIdleAsync(tenantId, serviceId, userId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("User is not assigned to any counter.", result.Error);
        _mockRedis.Verify(r => r.RemoveCounterAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseCounterIfIdleAsync_ShouldReturnServing_WhenCounterHasCalledTicket()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var userId = "user1";
        var counterNumber = "1";

        _mockRedis.Setup(r => r.GetUserCounterAsync(tenantId, serviceId, userId))
            .ReturnsAsync(counterNumber);

        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>
            {
                new QueueEntry { CounterId = counterNumber, Status = "CALLED" }
            });

        // Act
        var result = await _service.ReleaseCounterIfIdleAsync(tenantId, serviceId, userId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Counter is currently serving a ticket.", result.Error);
        _mockRedis.Verify(r => r.RemoveCounterAsync(tenantId, serviceId, userId), Times.Never);
    }

    [Fact]
    public async Task ReleaseCounterIfIdleAsync_ShouldRemoveCounter_WhenIdle()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var userId = "user1";
        var counterNumber = "1";

        _mockRedis.Setup(r => r.GetUserCounterAsync(tenantId, serviceId, userId))
            .ReturnsAsync(counterNumber);

        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>());

        // Act
        var result = await _service.ReleaseCounterIfIdleAsync(tenantId, serviceId, userId);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Error);
        _mockRedis.Verify(r => r.RemoveCounterAsync(tenantId, serviceId, userId), Times.Once);
    }

    [Fact]
    public async Task GetActiveCountersAsync_ShouldReturnFromRedis()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var activeCounters = new Dictionary<string, string>
        {
            { "1", "user1" },
            { "2", "user2" }
        };

        _mockRedis.Setup(r => r.GetActiveCountersAsync(tenantId, serviceId))
            .ReturnsAsync(activeCounters);

        // Act
        var result = await _service.GetActiveCountersAsync(tenantId, serviceId);

        // Assert
        Assert.Equal(activeCounters, result);
        _mockRedis.Verify(r => r.GetActiveCountersAsync(tenantId, serviceId), Times.Once);
    }
}
