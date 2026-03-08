using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using QueueService.Controllers;
using QueueService.Events;

namespace QueueService.Tests;

public class AppointmentEventControllerTests
{
    private readonly Mock<IEventBus> _mockBus;
    private readonly AppointmentEventController _controller;

    public AppointmentEventControllerTests()
    {
        _mockBus = new Mock<IEventBus>();
        _controller = new AppointmentEventController(_mockBus.Object);
    }

    [Fact]
    public async Task CheckIn_ShouldPublishEventAndReturnOk()
    {
        // Arrange
        var evt = new AppointmentCheckedInEvent(
            TenantId: Guid.NewGuid(),
            ServiceId: Guid.NewGuid(),
            AppointmentId: Guid.NewGuid(),
            PriorityLevel: 1
        );

        // Act
        var result = await _controller.CheckIn(evt);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("appointment_checked_in published", okResult.Value);
        _mockBus.Verify(b => b.Publish(evt), Times.Once);
    }
}