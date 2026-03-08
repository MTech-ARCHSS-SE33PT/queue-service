using Xunit;
using QueueService.Events;

namespace QueueService.Tests;

public class InMemoryEventBusTests
{
    private readonly InMemoryEventBus _bus;

    public InMemoryEventBusTests()
    {
        _bus = new InMemoryEventBus();
    }

    [Fact]
    public async Task Publish_ShouldCallSubscribedHandlers()
    {
        // Arrange
        var called = false;
        var evt = new AppointmentCheckedInEvent(
            TenantId: Guid.NewGuid(),
            ServiceId: Guid.NewGuid(),
            AppointmentId: Guid.NewGuid(),
            PriorityLevel: 1
        );

        _bus.Subscribe<AppointmentCheckedInEvent>(async e =>
        {
            called = true;
            Assert.Equal(evt, e);
        });

        // Act
        await _bus.Publish(evt);

        // Assert
        Assert.True(called);
    }

    [Fact]
    public async Task Publish_ShouldNotCallHandlersForDifferentEventType()
    {
        // Arrange
        var called = false;
        var evt = new AppointmentCheckedInEvent(
            TenantId: Guid.NewGuid(),
            ServiceId: Guid.NewGuid(),
            AppointmentId: Guid.NewGuid(),
            PriorityLevel: 1
        );

        _bus.Subscribe<string>(async e => called = true);

        // Act
        await _bus.Publish(evt);

        // Assert
        Assert.False(called);
    }

    [Fact]
    public async Task Publish_ShouldCallMultipleHandlers()
    {
        // Arrange
        var callCount = 0;
        var evt = new AppointmentCheckedInEvent(
            TenantId: Guid.NewGuid(),
            ServiceId: Guid.NewGuid(),
            AppointmentId: Guid.NewGuid(),
            PriorityLevel: 1
        );

        _bus.Subscribe<AppointmentCheckedInEvent>(async e => callCount++);
        _bus.Subscribe<AppointmentCheckedInEvent>(async e => callCount++);

        // Act
        await _bus.Publish(evt);

        // Assert
        Assert.Equal(2, callCount);
    }
}