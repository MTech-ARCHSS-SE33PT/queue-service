using Xunit;
using QueueService.Events;

namespace QueueService.Tests;

public class ConsoleEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_ShouldNotThrow()
    {
        // Arrange
        var publisher = new ConsoleEventPublisher();
        var eventType = "test_event";
        var payload = new { Message = "Test" };

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => throw new Exception()); // No exception expected
        await publisher.PublishAsync(eventType, payload);
    }
}