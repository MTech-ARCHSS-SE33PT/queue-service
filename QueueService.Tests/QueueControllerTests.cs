using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using QueueService.Controllers;
using QueueService.Services;
using QueueService.DTOs;

namespace QueueService.Tests;

public class QueueControllerTests
{
    private readonly Mock<QueueOrchestratorService> _mockOrchestrator;
    private readonly QueueController _controller;

    public QueueControllerTests()
    {
        _mockOrchestrator = new Mock<QueueOrchestratorService>();
        _controller = new QueueController(_mockOrchestrator.Object);
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

        _mockOrchestrator.Setup(o => o.Configure(tenantId, serviceId, serviceName, locationName, maxCounters))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Configure(tenantId, serviceId, serviceName, locationName, maxCounters);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockOrchestrator.Verify(o => o.Configure(tenantId, serviceId, serviceName, locationName, maxCounters), Times.Once);
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

        var expectedResult = new QueueEntryDto
        {
            Id = Guid.NewGuid(),
            TicketNumber = "A001",
            PriorityLevel = 1,
            Status = "WAITING"
        };

        _mockOrchestrator.Setup(o => o.CreateTicket(tenantId, serviceId, appointmentId, 1))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedDto = Assert.IsType<QueueEntryDto>(okResult.Value);
        Assert.Equal(expectedResult.Id, returnedDto.Id);
        Assert.Equal(expectedResult.TicketNumber, returnedDto.TicketNumber);
    }

    [Fact]
    public async Task CallNext_ReturnsTicket()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        var expectedTicket = new QueueEntryDto
        {
            Id = Guid.NewGuid(),
            TicketNumber = "A001",
            Status = "CALLED",
            CounterId = counterId
        };

        _mockOrchestrator.Setup(o => o.CallNext(tenantId, serviceId, counterId))
            .ReturnsAsync(expectedTicket);

        // Act
        var result = await _controller.Call(tenantId, serviceId, counterId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedTicket = Assert.IsType<QueueEntryDto>(okResult.Value);
        Assert.Equal(expectedTicket.Id, returnedTicket.Id);
        Assert.Equal(counterId, returnedTicket.CounterId);
    }

    [Fact]
    public async Task CallNext_ReturnsNotFoundWhenNoTickets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        _mockOrchestrator.Setup(o => o.CallNext(tenantId, serviceId, counterId))
            .ReturnsAsync((QueueEntryDto?)null);

        // Act
        var result = await _controller.Call(tenantId, serviceId, counterId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal("No waiting tickets.", notFoundResult.Value);
    }

    [Fact]
    public async Task CallNext_ReturnsBadRequestWhenCounterBusy()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var counterId = "Counter1";

        _mockOrchestrator.Setup(o => o.CallNext(tenantId, serviceId, counterId))
            .ThrowsAsync(new InvalidOperationException("Counter is busy"));

        // Act
        var result = await _controller.Call(tenantId, serviceId, counterId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Counter is busy", badRequestResult.Value);
    }

    [Fact]
    public async Task Complete_ReturnsOk()
    {
        // Arrange
        var queueEntryId = Guid.NewGuid();

        _mockOrchestrator.Setup(o => o.CompleteTicket(queueEntryId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Complete(queueEntryId);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockOrchestrator.Verify(o => o.CompleteTicket(queueEntryId), Times.Once);
    }

    [Fact]
    public async Task Status_ReturnsQueueStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var expectedStatus = new { WaitingCount = 5, Serving = new List<object>() };

        _mockOrchestrator.Setup(o => o.GetStatus(tenantId, serviceId))
            .ReturnsAsync(expectedStatus);

        // Act
        var result = await _controller.Status(tenantId, serviceId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedStatus, okResult.Value);
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 10;

        _mockOrchestrator.Setup(o => o.Update(tenantId, serviceId, maxCounters))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Update(tenantId, serviceId, maxCounters);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Queue updated successfully.", okResult.Value);
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

        _mockOrchestrator.Setup(o => o.Delete(tenantId, serviceId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(tenantId, serviceId);

        // Assert
        Assert.IsType<OkResult>(result);
        _mockOrchestrator.Verify(o => o.Delete(tenantId, serviceId), Times.Once);
    }
}