using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QueueService.Events;
using System.Text.Json;

namespace QueueService.Infrastructure.ServiceBus;

public sealed class ServiceBusInboundSubscriber : BackgroundService, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<ServiceBusInboundSubscriber> _logger;
    private readonly ServiceBusProcessor _processor;
    private readonly IEventBus _eventBus;
    private readonly InboundMessageStore _store;

    public ServiceBusInboundSubscriber(
        ServiceBusClient client,
        ServiceBusSettings settings,
        IEventBus eventBus,
        InboundMessageStore store,
        ILogger<ServiceBusInboundSubscriber> logger)
    {
        _logger = logger;
        _eventBus = eventBus;
        _store = store;

        if (string.IsNullOrWhiteSpace(settings.TopicName))
            throw new ArgumentException("ServiceBusSettings.TopicName is required for inbound subscription.");
        if (string.IsNullOrWhiteSpace(settings.SubscriptionName))
            throw new ArgumentException("ServiceBusSettings.SubscriptionName is required for inbound subscription.");

        _logger.LogInformation(
            "Service Bus inbound subscription configured. Topic={TopicName} Subscription={SubscriptionName}",
            settings.TopicName,
            settings.SubscriptionName);

        _processor = client.CreateProcessor(
            settings.TopicName,
            settings.SubscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += OnMessage;
        _processor.ProcessErrorAsync += OnError;

        _logger.LogInformation("Starting Service Bus processor...");
        await _processor.StartProcessingAsync(stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }

        _logger.LogInformation("Stopping Service Bus processor...");
        await _processor.StopProcessingAsync();
    }

    private async Task OnMessage(ProcessMessageEventArgs args)
    {
        var subject = args.Message.Subject;
        var receivedAt = DateTimeOffset.UtcNow;

        try
        {
            if (string.Equals(subject, "appointment_checked_in", StringComparison.OrdinalIgnoreCase))
            {
                var evt = JsonSerializer.Deserialize<AppointmentCheckedInEvent>(
                    args.Message.Body.ToString(),
                    JsonOptions);
                if (evt is null)
                    throw new InvalidOperationException("Failed to deserialize AppointmentCheckedInEvent.");

                _store.RecordAppointmentCheckedIn(subject, args.Message.MessageId, receivedAt, evt.TenantId, evt.ServiceId, evt.AppointmentId);
                await _eventBus.Publish(evt);
            }
            else
            {
                _store.Record(subject, args.Message.MessageId, receivedAt);
                _logger.LogDebug("Ignoring message with Subject={Subject} MessageId={MessageId}", subject, args.Message.MessageId);
            }

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing Service Bus message. Subject={Subject} MessageId={MessageId}", subject, args.Message.MessageId);

            try
            {
                await args.DeadLetterMessageAsync(
                    args.Message,
                    deadLetterReason: "processing_failed",
                    deadLetterErrorDescription: ex.Message,
                    cancellationToken: args.CancellationToken);
            }
            catch (Exception dlqEx)
            {
                _logger.LogError(dlqEx, "Failed dead-lettering Service Bus message. MessageId={MessageId}", args.Message.MessageId);
            }
        }
    }

    private Task OnError(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus processor error. Entity={EntityPath} ErrorSource={ErrorSource}",
            args.EntityPath,
            args.ErrorSource);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
    }
}
