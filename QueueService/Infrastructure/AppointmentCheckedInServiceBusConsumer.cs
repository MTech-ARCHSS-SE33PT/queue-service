using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QueueService.Events;

namespace QueueService.Infrastructure;

public sealed class AppointmentCheckedInServiceBusConsumer : BackgroundService, IAsyncDisposable
{
    private readonly ServiceBusProcessor _processor;
    private readonly IEventBus _bus;
    private readonly ILogger<AppointmentCheckedInServiceBusConsumer> _log;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public AppointmentCheckedInServiceBusConsumer(
        ServiceBusClient client,
        IConfiguration configuration,
        IEventBus bus,
        ILogger<AppointmentCheckedInServiceBusConsumer> log)
    {
        _bus = bus;
        _log = log;

        var topic = ServiceBusConfig.GetTopic(configuration);
        var subscription = ServiceBusConfig.GetSubscription(configuration);
        if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(subscription))
            throw new InvalidOperationException("ServiceBus topic/subscription is not configured (ServiceBus:Topic, ServiceBus:Subscription).");

        _processor = client.CreateProcessor(topic, subscription, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _processor.StartProcessingAsync(stoppingToken);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // host is stopping
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Service Bus consumer failed to start.");
        }
        finally
        {
            try
            {
                await _processor.StopProcessingAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Service Bus consumer failed to stop.");
            }
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var eventType = args.Message.Subject;
        if (string.IsNullOrWhiteSpace(eventType) &&
            args.Message.ApplicationProperties.TryGetValue("eventType", out var et) &&
            et is string etStr)
        {
            eventType = etStr;
        }

        try
        {
            var json = args.Message.Body.ToString();
            if (!IsAppointmentCheckedIn(eventType) && !string.IsNullOrWhiteSpace(eventType))
            {
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            var evt = JsonSerializer.Deserialize<AppointmentCheckedInEvent>(json, _jsonOptions);
            if (evt is null)
                throw new JsonException("Deserialized to null.");

            await _bus.Publish(evt);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                _log.LogWarning(ex, "Ignoring message {MessageId} with no eventType/subject.", args.Message.MessageId);
                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            _log.LogError(ex, "Failed to process AppointmentCheckedIn message {MessageId}.", args.Message.MessageId);
            await args.DeadLetterMessageAsync(
                args.Message,
                deadLetterReason: "processing_failed",
                deadLetterErrorDescription: ex.Message,
                cancellationToken: args.CancellationToken);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _log.LogError(args.Exception, "Service Bus processing error. Entity={EntityPath}", args.EntityPath);
        return Task.CompletedTask;
    }

    private static bool IsAppointmentCheckedIn(string? eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            return false;

        return eventType.Equals("appointment_checked_in", StringComparison.OrdinalIgnoreCase) ||
               eventType.Equals(nameof(AppointmentCheckedInEvent), StringComparison.OrdinalIgnoreCase);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
    }
}
