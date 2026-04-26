using Microsoft.AspNetCore.Mvc;
using Moq;
using QueueService.Controllers;
using QueueService.DTOs;
using QueueService.Events;
using QueueService.Models;
using QueueService.Repositories;
using QueueService.Services;
using Xunit;

namespace QueueService.Tests;

public class QueueControllerTests
{
    private readonly Mock<IQueueRepository> _repository = new();
    private readonly Mock<IRedisQueueService> _redis = new();
    private readonly Mock<IEventPublisher> _publisher = new();
    private readonly QueueController _controller;

    public QueueControllerTests()
    {
        var orchestrator = new QueueOrchestratorService(
            _repository.Object,
            _redis.Object,
            _publisher.Object);
        _controller = new QueueController(orchestrator);
    }

    [Fact]
    public async Task Configure_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var serviceName = "Test Service";
        var locationName = "Test Location";
        var maxCounters = 5;

        var result = await _controller.Configure(tenantId, serviceId, serviceName, locationName, maxCounters);

        Assert.IsType<OkResult>(result);
        _repository.Verify(r => r.ConfigureAsync(tenantId, serviceId, serviceName, locationName, maxCounters), Times.Once);
    }

    [Fact]
    public async Task CreateTicket_ReturnsCreatedTicket()
    {
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
            Status = "WAITING"
        };
        _repository.Setup(r => r.CreateTicketAsync(tenantId, serviceId, appointmentId, 1))
            .ReturnsAsync(ticket);

        var result = await _controller.Create(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedDto = Assert.IsType<QueueEntryDto>(okResult.Value);
        Assert.Equal(ticket.Id, returnedDto.Id);
        Assert.Equal(ticket.TicketNumber, returnedDto.TicketNumber);
        _redis.Verify(r => r.EnqueueAsync(ticket), Times.Once);
        _publisher.Verify(p => p.PublishAsync("ticket_created", It.IsAny<TicketCreatedEvent>()), Times.Once);
        _publisher.Verify(p => p.PublishAsync("queue_updated", It.IsAny<QueueUpdatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task CallNext_ReturnsTicket()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";
        var ticket = new QueueEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = serviceId,
            TicketNumber = "A001",
            Status = "CALLED",
            CounterId = counterId
        };

        _repository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>());
        _redis.Setup(r => r.DequeueNextAsync(tenantId, serviceId))
            .ReturnsAsync(ticket.Id);
        _repository.Setup(r => r.MarkAsCalledAsync(ticket.Id, counterId))
            .ReturnsAsync(ticket);

        var result = await _controller.Call(tenantId, serviceId, counterId);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedTicket = Assert.IsType<QueueEntryDto>(okResult.Value);
        Assert.Equal(ticket.Id, returnedTicket.Id);
        Assert.Equal(counterId, returnedTicket.CounterId);
    }

    [Fact]
    public async Task CallNext_ReturnsNotFoundWhenNoTickets()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        _repository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry>());
        _redis.Setup(r => r.DequeueNextAsync(tenantId, serviceId))
            .ReturnsAsync((Guid?)null);
        _repository.Setup(r => r.GetNextWaitingTicketAsync(tenantId, serviceId))
            .ReturnsAsync((QueueEntry?)null);

        var result = await _controller.Call(tenantId, serviceId, counterId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("No waiting tickets.", notFoundResult.Value);
    }

    [Fact]
    public async Task CallNext_ReturnsBadRequestWhenCounterBusy()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        _repository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(new List<QueueEntry> { new() { CounterId = counterId } });

        var result = await _controller.Call(tenantId, serviceId, counterId);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("This counter is already serving a ticket.", badRequestResult.Value);
    }

    [Fact]
    public async Task Complete_ReturnsOk()
    {
        var queueEntryId = Guid.NewGuid();
        var ticket = new QueueEntry
        {
            Id = queueEntryId,
            TenantId = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            ServedAt = DateTime.UtcNow
        };
        _repository.Setup(r => r.MarkAsCompletedAsync(queueEntryId))
            .ReturnsAsync(ticket);

        var result = await _controller.Complete(queueEntryId);

        Assert.IsType<OkResult>(result);
        _repository.Verify(r => r.MarkAsCompletedAsync(queueEntryId), Times.Once);
    }

    [Fact]
    public async Task Status_ReturnsQueueStatus()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var servingTickets = new List<QueueEntry>();

        _redis.Setup(r => r.GetWaitingCountAsync(tenantId, serviceId))
            .ReturnsAsync(5);
        _repository.Setup(r => r.GetServingTicketsAsync(tenantId, serviceId))
            .ReturnsAsync(servingTickets);

        var result = await _controller.Status(tenantId, serviceId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 10;

        var result = await _controller.Update(tenantId, serviceId, maxCounters);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Queue updated successfully.", okResult.Value);
        _repository.Verify(r => r.UpdateAsync(tenantId, serviceId, maxCounters), Times.Once);
    }

    [Fact]
    public async Task Update_ReturnsBadRequestWhenMaxCountersInvalid()
    {
        var result = await _controller.Update(Guid.NewGuid(), Guid.NewGuid(), 0);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("MaxCounters must be at least 1.", badRequestResult.Value);
    }

    [Fact]
    public async Task Delete_ReturnsOk()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var result = await _controller.Delete(tenantId, serviceId);

        Assert.IsType<OkObjectResult>(result);
        _repository.Verify(r => r.DeleteAsync(tenantId, serviceId), Times.Once);
        _redis.Verify(r => r.RemoveQueueAsync(tenantId, serviceId), Times.Once);
    }
}
