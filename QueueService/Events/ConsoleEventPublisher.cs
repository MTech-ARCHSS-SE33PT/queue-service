using System.Text.Json;

namespace QueueService.Events;

public class ConsoleEventPublisher : IEventPublisher
{
    public Task PublishAsync<T>(string eventType, T payload)
    {
        Console.WriteLine("=================================");
        Console.WriteLine($"EVENT: {eventType}");
        Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("=================================");
        return Task.CompletedTask;
    }
}
