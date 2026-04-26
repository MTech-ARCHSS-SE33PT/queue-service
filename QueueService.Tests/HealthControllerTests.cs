using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using QueueService.Controllers;

namespace QueueService.Tests;

public sealed class HealthControllerTests
{
    [Fact]
    public void ServiceBusConfig_ReturnsMaskedConnectionStringAndNames()
    {
        using var env = new EnvironmentScope(("ASB_CONN", null), ("ASB_TOPIC", null), ("ASB_SUB", null));
        var controller = new HealthController(Configuration(new Dictionary<string, string?>
        {
            ["ASB_CONN"] = "Endpoint=sb://queue-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=secret",
            ["ASB_TOPIC"] = "appointments",
            ["ASB_SUB"] = "queue-service"
        }));

        var result = controller.ServiceBusConfig();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.SerializeToElement(ok.Value);
        Assert.True(json.GetProperty("ok").GetBoolean());
        Assert.Equal("appointments", json.GetProperty("topicName").GetString());
        Assert.Equal("queue-service", json.GetProperty("subscriptionName").GetString());
        Assert.Equal("sb://queue-test.servicebus.windows.net/", json.GetProperty("serviceBus").GetProperty("endpoint").GetString());
        Assert.Equal("RootManageSharedAccessKey", json.GetProperty("serviceBus").GetProperty("sharedAccessKeyName").GetString());
    }

    [Fact]
    public async Task ServiceBusTopics_WithInvalidConnection_ReturnsServiceUnavailable()
    {
        var controller = new HealthController(Configuration(new Dictionary<string, string?>
        {
            ["ASB_CONN"] = "not-a-service-bus-connection"
        }));

        var result = await controller.ServiceBusTopics(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task ServiceBusSubscriptions_ValidatesTopicAndHandlesInvalidConnection()
    {
        var emptyController = new HealthController(Configuration());

        var badRequest = await emptyController.ServiceBusSubscriptions("", CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(badRequest);

        var invalidConnectionController = new HealthController(Configuration(new Dictionary<string, string?>
        {
            ["ASB_CONN"] = "not-a-service-bus-connection"
        }));
        var unavailable = await invalidConnectionController.ServiceBusSubscriptions("appointments", CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(unavailable);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task ServiceBusSubscriptionRuntime_ValidatesInputsAndHandlesInvalidConnection()
    {
        var emptyController = new HealthController(Configuration());

        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusSubscriptionRuntime("", "sub", CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusSubscriptionRuntime("topic", "", CancellationToken.None));

        var invalidConnectionController = new HealthController(Configuration(new Dictionary<string, string?>
        {
            ["ASB_CONN"] = "not-a-service-bus-connection"
        }));
        var unavailable = await invalidConnectionController.ServiceBusSubscriptionRuntime("topic", "sub", CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(unavailable);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task ServiceBusRules_ValidatesInputsAndHandlesInvalidConnection()
    {
        var emptyController = new HealthController(Configuration());

        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusRules("", "sub", CancellationToken.None));
        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusRules("topic", "", CancellationToken.None));

        var invalidConnectionController = new HealthController(Configuration(new Dictionary<string, string?>
        {
            ["ASB_CONN"] = "not-a-service-bus-connection"
        }));
        var unavailable = await invalidConnectionController.ServiceBusRules("topic", "sub", CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(unavailable);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    [Fact]
    public async Task ServiceBusDlqPeek_ValidatesInputsAndHandlesInvalidConnection()
    {
        var emptyController = new HealthController(Configuration());

        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusDlqPeek("", "sub"));
        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusDlqPeek("topic", ""));
        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusDlqPeek("topic", "sub", max: 0));
        Assert.IsType<BadRequestObjectResult>(
            await emptyController.ServiceBusDlqPeek("topic", "sub", max: 51));

        var invalidConnectionController = new HealthController(Configuration(new Dictionary<string, string?>
        {
            ["ASB_CONN"] = "not-a-service-bus-connection"
        }));
        var unavailable = await invalidConnectionController.ServiceBusDlqPeek("topic", "sub");

        var status = Assert.IsType<ObjectResult>(unavailable);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    private static IConfiguration Configuration(Dictionary<string, string?>? values = null)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? new Dictionary<string, string?>())
            .Build();

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originals;

        public EnvironmentScope(params (string Key, string? Value)[] values)
        {
            _originals = values.ToDictionary(x => x.Key, x => Environment.GetEnvironmentVariable(x.Key));
            foreach (var (key, value) in values)
                Environment.SetEnvironmentVariable(key, value);
        }

        public void Dispose()
        {
            foreach (var (key, value) in _originals)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}
