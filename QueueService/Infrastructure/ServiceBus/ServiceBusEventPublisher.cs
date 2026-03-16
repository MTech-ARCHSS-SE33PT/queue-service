using System.Text.Json;
using Azure.Messaging.ServiceBus;
using QueueService.Events;

namespace QueueService.Infrastructure.ServiceBus;

public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ServiceBusSender _sender;

    public ServiceBusEventPublisher(ServiceBusClient client, string topicName)
    {
        _sender = client.CreateSender(topicName);
    }

    public async Task PublishAsync<T>(string eventType, T payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        var msg = new ServiceBusMessage(json)
        {
            ContentType = "application/json",
            Subject = eventType,
        };

        await _sender.SendMessageAsync(msg);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}
