using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.AspNetCore.Mvc;
using QueueService.Infrastructure;

namespace QueueService.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public HealthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string? ResolveServiceBusConnectionString()
    {
        DotEnv.LoadFromWellKnownLocations();

        var connectionString =
            FirstNonEmpty(
                _configuration["ASB_CONN"],
                Environment.GetEnvironmentVariable("ASB_CONN"),
                _configuration["ServiceBus:ConnectionString"],
                Environment.GetEnvironmentVariable("ServiceBus__ConnectionString"),
                _configuration.GetConnectionString("ServiceBus"));

        if (string.IsNullOrWhiteSpace(connectionString) &&
            DotEnv.TryGetValueFromWellKnownLocations("ASB_CONN", out var asbConnFromFile) &&
            !string.IsNullOrWhiteSpace(asbConnFromFile))
        {
            connectionString = asbConnFromFile;
        }

        return connectionString;
    }

    private string? ResolveServiceBusTopicName()
    {
        DotEnv.LoadFromWellKnownLocations();

        var topicName =
            FirstNonEmpty(
                _configuration["ASB_TOPIC"],
                Environment.GetEnvironmentVariable("ASB_TOPIC"),
                _configuration["ServiceBus:TopicName"]);

        if (string.IsNullOrWhiteSpace(topicName) &&
            DotEnv.TryGetValueFromWellKnownLocations("ASB_TOPIC", out var asbTopicFromFile) &&
            !string.IsNullOrWhiteSpace(asbTopicFromFile))
        {
            topicName = asbTopicFromFile;
        }

        return topicName;
    }

    private string? ResolveServiceBusSubscriptionName()
    {
        DotEnv.LoadFromWellKnownLocations();

        var subscriptionName =
            FirstNonEmpty(
                _configuration["ASB_SUB"],
                Environment.GetEnvironmentVariable("ASB_SUB"),
                _configuration["ServiceBus:SubscriptionName"]);

        if (string.IsNullOrWhiteSpace(subscriptionName) &&
            DotEnv.TryGetValueFromWellKnownLocations("ASB_SUB", out var asbSubFromFile) &&
            !string.IsNullOrWhiteSpace(asbSubFromFile))
        {
            subscriptionName = asbSubFromFile;
        }

        return subscriptionName;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }
        return null;
    }

    private static object MaskServiceBusConnectionString(string connectionString)
    {
        static string? Extract(string key, string s)
        {
            var idx = s.IndexOf($"{key}=", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            idx += key.Length + 1;
            var end = s.IndexOf(';', idx);
            if (end < 0) end = s.Length;
            return s[idx..end];
        }

        var endpoint = Extract("Endpoint", connectionString);
        var keyName = Extract("SharedAccessKeyName", connectionString);

        return new
        {
            hasConnectionString = !string.IsNullOrWhiteSpace(connectionString),
            endpoint,
            sharedAccessKeyName = keyName
        };
    }

    [HttpGet("servicebus/config")]
    public IActionResult ServiceBusConfig()
    {
        DotEnv.LoadFromWellKnownLocations();

        var connectionString =
            FirstNonEmpty(
                _configuration["ASB_CONN"],
                Environment.GetEnvironmentVariable("ASB_CONN"),
                _configuration["ServiceBus:ConnectionString"],
                Environment.GetEnvironmentVariable("ServiceBus__ConnectionString"),
                _configuration.GetConnectionString("ServiceBus"));

        if (string.IsNullOrWhiteSpace(connectionString) &&
            DotEnv.TryGetValueFromWellKnownLocations("ASB_CONN", out var asbConnFromFile) &&
            !string.IsNullOrWhiteSpace(asbConnFromFile))
        {
            connectionString = asbConnFromFile;
        }

        var topicName =
            FirstNonEmpty(
                _configuration["ASB_TOPIC"],
                Environment.GetEnvironmentVariable("ASB_TOPIC"),
                _configuration["ServiceBus:TopicName"]);

        if (string.IsNullOrWhiteSpace(topicName) &&
            DotEnv.TryGetValueFromWellKnownLocations("ASB_TOPIC", out var asbTopicFromFile) &&
            !string.IsNullOrWhiteSpace(asbTopicFromFile))
        {
            topicName = asbTopicFromFile;
        }

        var subscriptionName =
            FirstNonEmpty(
                _configuration["ASB_SUB"],
                Environment.GetEnvironmentVariable("ASB_SUB"),
                _configuration["ServiceBus:SubscriptionName"]);

        if (string.IsNullOrWhiteSpace(subscriptionName) &&
            DotEnv.TryGetValueFromWellKnownLocations("ASB_SUB", out var asbSubFromFile) &&
            !string.IsNullOrWhiteSpace(asbSubFromFile))
        {
            subscriptionName = asbSubFromFile;
        }

        return Ok(new
        {
            ok = true,
            serviceBus = connectionString is null ? null : MaskServiceBusConnectionString(connectionString),
            topicName,
            subscriptionName
        });
    }

    [HttpGet("servicebus")]
    public async Task<IActionResult> ServiceBus(
        [FromQuery] string? queueName,
        [FromQuery] string? topicName,
        [FromQuery] string? subscriptionName,
        CancellationToken cancellationToken)
    {
        var connectionString = ResolveServiceBusConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    ok = false,
                    error = "Missing ServiceBus connection string. Set ServiceBus:ConnectionString (or ConnectionStrings:ServiceBus)."
                });
        }

        queueName ??= _configuration["ServiceBus:QueueName"];
        topicName ??= ResolveServiceBusTopicName();
        subscriptionName ??= ResolveServiceBusSubscriptionName();

        try
        {
            if (!string.IsNullOrWhiteSpace(queueName))
            {
                await using var client = new ServiceBusClient(connectionString);
                await using var receiver = client.CreateReceiver(queueName);

                var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
                if (msg is not null)
                    await receiver.AbandonMessageAsync(msg, cancellationToken: cancellationToken);

                return Ok(new { ok = true, method = "receive", entity = $"queue:{queueName}" });
            }

            if (!string.IsNullOrWhiteSpace(topicName) && !string.IsNullOrWhiteSpace(subscriptionName))
            {
                await using var client = new ServiceBusClient(connectionString);
                await using var receiver = client.CreateReceiver(topicName, subscriptionName);

                var msg = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2), cancellationToken);
                if (msg is not null)
                    await receiver.AbandonMessageAsync(msg, cancellationToken: cancellationToken);

                return Ok(new { ok = true, method = "receive", entity = $"topic:{topicName}/subscriptions:{subscriptionName}" });
            }

            var admin = new ServiceBusAdministrationClient(connectionString);
            var ns = await admin.GetNamespacePropertiesAsync(cancellationToken);

            return Ok(new { ok = true, method = "admin", entity = $"namespace:{ns.Value.Name}" });
        }
        catch (RequestFailedException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = ex.Message, status = ex.Status });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = ex.Message });
        }
        catch (ServiceBusException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = ex.Message, reason = ex.Reason.ToString() });
        }
    }

    [HttpGet("servicebus/topics")]
    public async Task<IActionResult> ServiceBusTopics(CancellationToken cancellationToken)
    {
        var connectionString = ResolveServiceBusConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = "Missing ServiceBus connection string." });
        }

        try
        {
            var admin = new ServiceBusAdministrationClient(connectionString);
            var topics = new List<string>();

            await foreach (var topic in admin.GetTopicsAsync(cancellationToken))
                topics.Add(topic.Name);

            return Ok(new { ok = true, topics });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, error = ex.Message });
        }
    }

    [HttpGet("servicebus/subscriptions")]
    public async Task<IActionResult> ServiceBusSubscriptions([FromQuery] string topicName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return BadRequest(new { ok = false, error = "topicName is required" });

        DotEnv.LoadFromWellKnownLocations();

        var connectionString = ResolveServiceBusConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = "Missing ServiceBus connection string." });
        }

        try
        {
            var admin = new ServiceBusAdministrationClient(connectionString);
            var subs = new List<string>();

            await foreach (var sub in admin.GetSubscriptionsAsync(topicName, cancellationToken))
                subs.Add(sub.SubscriptionName);

            return Ok(new { ok = true, topic = topicName, subscriptions = subs });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, error = ex.Message });
        }
    }

    [HttpGet("servicebus/subscription-runtime")]
    public async Task<IActionResult> ServiceBusSubscriptionRuntime(
        [FromQuery] string topicName,
        [FromQuery] string subscriptionName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return BadRequest(new { ok = false, error = "topicName is required" });
        if (string.IsNullOrWhiteSpace(subscriptionName))
            return BadRequest(new { ok = false, error = "subscriptionName is required" });

        DotEnv.LoadFromWellKnownLocations();

        var connectionString = ResolveServiceBusConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = "Missing ServiceBus connection string." });
        }

        try
        {
            var admin = new ServiceBusAdministrationClient(connectionString);
            var props = await admin.GetSubscriptionRuntimePropertiesAsync(topicName, subscriptionName, cancellationToken);

            return Ok(new
            {
                ok = true,
                topic = topicName,
                subscription = subscriptionName,
                active = props.Value.ActiveMessageCount,
                deadLetter = props.Value.DeadLetterMessageCount,
                transfer = props.Value.TransferMessageCount,
                transferDeadLetter = props.Value.TransferDeadLetterMessageCount,
                total = props.Value.TotalMessageCount
            });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, error = ex.Message });
        }
    }

    [HttpGet("servicebus/rules")]
    public async Task<IActionResult> ServiceBusRules(
        [FromQuery] string topicName,
        [FromQuery] string subscriptionName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return BadRequest(new { ok = false, error = "topicName is required" });
        if (string.IsNullOrWhiteSpace(subscriptionName))
            return BadRequest(new { ok = false, error = "subscriptionName is required" });

        DotEnv.LoadFromWellKnownLocations();

        var connectionString = ResolveServiceBusConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = "Missing ServiceBus connection string." });
        }

        try
        {
            var admin = new ServiceBusAdministrationClient(connectionString);
            var rules = new List<object>();

            await foreach (var rule in admin.GetRulesAsync(topicName, subscriptionName, cancellationToken))
            {
                object filter = rule.Filter switch
                {
                    SqlRuleFilter sql => new { type = "sql", expression = sql.SqlExpression },
                    CorrelationRuleFilter corr => new
                    {
                        type = "correlation",
                        subject = corr.Subject,
                        correlationId = corr.CorrelationId
                    },
                    _ => new { type = rule.Filter.GetType().Name }
                };

                rules.Add(new
                {
                    name = rule.Name,
                    filter,
                    action = rule.Action is SqlRuleAction sqlAction ? sqlAction.SqlExpression : null
                });
            }

            return Ok(new { ok = true, topic = topicName, subscription = subscriptionName, rules });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, error = ex.Message });
        }
    }

    [HttpGet("servicebus/dlq-peek")]
    public async Task<IActionResult> ServiceBusDlqPeek(
        [FromQuery] string topicName,
        [FromQuery] string subscriptionName,
        [FromQuery] int max = 10,
        [FromQuery] bool includeBody = false,
        [FromQuery] string? subject = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(topicName))
            return BadRequest(new { ok = false, error = "topicName is required" });
        if (string.IsNullOrWhiteSpace(subscriptionName))
            return BadRequest(new { ok = false, error = "subscriptionName is required" });
        if (max < 1 || max > 50)
            return BadRequest(new { ok = false, error = "max must be between 1 and 50" });

        DotEnv.LoadFromWellKnownLocations();

        var connectionString = ResolveServiceBusConnectionString();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { ok = false, error = "Missing ServiceBus connection string." });
        }

        try
        {
            await using var client = new ServiceBusClient(connectionString);
            await using var receiver = client.CreateReceiver(
                topicName,
                subscriptionName,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

            var peekCount = Math.Min(250, max * 10);
            var peeked = await receiver.PeekMessagesAsync(peekCount, cancellationToken: cancellationToken);
            var messages = string.IsNullOrWhiteSpace(subject)
                ? peeked.Take(max).ToList()
                : peeked.Where(m => string.Equals(m.Subject, subject, StringComparison.OrdinalIgnoreCase)).Take(max).ToList();

            var items = messages.Select(m =>
            {
                m.ApplicationProperties.TryGetValue("DeadLetterReason", out var reason);
                m.ApplicationProperties.TryGetValue("DeadLetterErrorDescription", out var desc);

                return new
                {
                    messageId = m.MessageId,
                    subject = m.Subject,
                    enqueuedTimeUtc = m.EnqueuedTime.UtcDateTime,
                    deliveryCount = m.DeliveryCount,
                    deadLetterReason = reason?.ToString(),
                    deadLetterErrorDescription = desc?.ToString(),
                    bodyPreview = includeBody ? m.Body.ToString()[..Math.Min(m.Body.ToString().Length, 500)] : null
                };
            });

            return Ok(new { ok = true, topic = topicName, subscription = subscriptionName, count = messages.Count, items });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { ok = false, error = ex.Message });
        }
    }
}
