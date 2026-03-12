using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QueueService.Events;

namespace QueueService.Infrastructure;

public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusEventPublisher> _log;

    public ServiceBusEventPublisher(
        ServiceBusClient client,
        IConfiguration configuration,
        ILogger<ServiceBusEventPublisher> log)
    {
        _log = log;

        var topic = ServiceBusConfig.GetTopic(configuration);
        if (string.IsNullOrWhiteSpace(topic))
            throw new InvalidOperationException("ServiceBus topic is not configured (ServiceBus:Topic).");

        _sender = client.CreateSender(topic);
    }

    public async Task PublishAsync<T>(string eventType, T payload)
    {
        try
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var message = new ServiceBusMessage(BinaryData.FromString(json))
            {
                Subject = eventType,
                ContentType = "application/json"
            };
            message.ApplicationProperties["eventType"] = eventType;
            message.ApplicationProperties["dotnetType"] = typeof(T).FullName ?? typeof(T).Name;

            await _sender.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to publish Service Bus event {EventType}.", eventType);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}

