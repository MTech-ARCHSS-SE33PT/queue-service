using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using QueueService.Controllers;
using QueueService.Services;
using QueueService.Repositories;
using QueueService.Events;
using QueueService.DTOs;
using QueueService.Models;

namespace QueueService.Tests;

public class QueueControllerTests
{
    private readonly Mock<IQueueRepository> _mockRepository;
    private readonly Mock<IRedisQueueService> _mockRedis;
    private readonly Mock<IEventPublisher> _mockPublisher;
    private readonly QueueController _controller;

    public QueueControllerTests()
    {
        _mockRepository = new Mock<IQueueRepository>();
        _mockRedis = new Mock<IRedisQueueService>();
        _mockPublisher = new Mock<IEventPublisher>();

        var orchestrator = new QueueOrchestratorService(
            _mockRepository.Object,
            _mockRedis.Object,
            _mockPublisher.Object);

        _controller = new QueueController(orchestrator);
    }

    [Fact]
    public async Task Configure_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var serviceName = "Test Service";
        var locationName = "Test Location";
        var maxCounters = 5;

        // Act
        var result = await _controller.Configure(tenantId, serviceId, serviceName, locationName, maxCounters);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockRepository.Verify(r => r.ConfigureAsync(
            tenantId, serviceId, serviceName, locationName, maxCounters), Times.Once);
    }

    [Fact]
    public async Task CreateTicket_ReturnsCreatedTicket()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var request = new CreateTicketRequest
        {
            TenantId = tenantId,
            ServiceId = serviceId,
            AppointmentId = appointmentId,
            Priority = 1
        };

        var ticket = new QueueEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = serviceId,
            AppointmentId = appointmentId,
            TicketNumber = "A001",
            PriorityLevel = 1,
            Status = "WAITING",
            EnqueuedAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.CreateTicketAsync(tenantId, serviceId, appointmentId, 1))
            .ReturnsAsync(ticket);
        _mockRedis.Setup(r => r.EnqueueAsync(ticket)).Returns(Task.CompletedTask);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDto = Assert.IsType<QueueEntryDto>(okResult.Value);
        Assert.Equal(ticket.Id, returnedDto.Id);
        Assert.Equal(ticket.TicketNumber, returnedDto.TicketNumber);
    }

    [Fact]
    public async Task CallNext_ReturnsTicket()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        var ticketId = Guid.NewGuid();
        var calledTicket = new QueueEntry
        {
            Id = ticketId,
            TenantId = tenantId,
            ServiceId = serviceId,
            TicketNumber = "A001",
            Status = "CALLED",
            CounterId = counterId,
            CalledAt = DateTime.UtcNow
        };

        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>());
        _mockRedis.Setup(r => r.DequeueNextAsync(tenantId, serviceId))
            .ReturnsAsync(ticketId);
        _mockRepository.Setup(r => r.MarkAsCalledAsync(ticketId, counterId))
            .ReturnsAsync(calledTicket);
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Call(tenantId, serviceId, counterId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTicket = Assert.IsType<QueueEntryDto>(okResult.Value);
        Assert.Equal(calledTicket.Id, returnedTicket.Id);
        Assert.Equal(counterId, returnedTicket.CounterId);
    }

    [Fact]
    public async Task CallNext_ReturnsNotFoundWhenNoTickets()
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
        var result = await _controller.Call(tenantId, serviceId, counterId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("No waiting tickets.", notFoundResult.Value);
    }

    [Fact]
    public async Task CallNext_ReturnsBadRequestWhenCounterBusy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>
            {
                new QueueEntry { CounterId = counterId, Status = "CALLED" }
            });

        // Act
        var result = await _controller.Call(tenantId, serviceId, counterId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("This counter is already serving a ticket.", badRequestResult.Value);
    }

    [Fact]
    public async Task Complete_ReturnsOk()
    {
        // Arrange
        var queueEntryId = Guid.NewGuid();

        _mockRepository.Setup(r => r.MarkAsCompletedAsync(queueEntryId))
            .ReturnsAsync(new QueueEntry
            {
                Id = queueEntryId,
                TenantId = Guid.NewGuid(),
                ServiceId = Guid.NewGuid(),
                ServedAt = DateTime.UtcNow
            });
        _mockPublisher.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Complete(queueEntryId);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockRepository.Verify(r => r.MarkAsCompletedAsync(queueEntryId), Times.Once);
    }

    [Fact]
    public async Task Status_ReturnsQueueStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        _mockRedis.Setup(r => r.GetWaitingCountAsync(tenantId, serviceId))
            .ReturnsAsync(5);
        _mockRepository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>());

        // Act
        var result = await _controller.Status(tenantId, serviceId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        var valueType = okResult.Value!.GetType();
        Assert.Equal(5L, valueType.GetProperty("WaitingCount")!.GetValue(okResult.Value));
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 10;

        // Act
        var result = await _controller.Update(tenantId, serviceId, maxCounters);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Queue updated successfully.", okResult.Value);
        _mockRepository.Verify(r => r.UpdateAsync(tenantId, serviceId, maxCounters), Times.Once);
    }

    [Fact]
    public async Task Update_ReturnsBadRequestWhenMaxCountersInvalid()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 0; // Invalid

        // Act
        var result = await _controller.Update(tenantId, serviceId, maxCounters);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("MaxCounters must be at least 1.", badRequestResult.Value);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        // Act
        var result = await _controller.Delete(tenantId, serviceId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Queue deleted successfully.", okResult.Value);
        _mockRepository.Verify(r => r.DeleteAsync(tenantId, serviceId), Times.Once);
        _mockRedis.Verify(r => r.RemoveQueueAsync(tenantId, serviceId), Times.Once);
    }
}
