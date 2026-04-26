using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using QueueService.Controllers;
using QueueService.Infrastructure.ServiceBus;
using QueueService.Events;

namespace QueueService.Tests;

public sealed class QueueApiIntegrationTests
{
    [Fact]
    public async Task ApiHost_StartsAndServesHealthConfig()
    {
        await using var factory = new WebApplicationFactory<QueueController>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ASB_CONN"] = "Endpoint=sb://queue-test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=secret",
                        ["ASB_TOPIC"] = "queue-tests",
                        ["ASB_SUB"] = "queue-tests",
                        ["ConnectionStrings:Redis"] = ""
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IHostedService>();
                });
            });

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/servicebus/config");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("queue-tests", json);
    }

    [Fact]
    public void ServiceBusInboundSubscriber_ValidatesRequiredSettingsBeforeCreatingProcessor()
    {
        var eventBus = new InMemoryEventBus();
        var store = new InboundMessageStore();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<ServiceBusInboundSubscriber>.Instance;
        var client = (Azure.Messaging.ServiceBus.ServiceBusClient)null!;

        Assert.Throws<ArgumentException>(() => new ServiceBusInboundSubscriber(
            client,
            new ServiceBusSettings { SubscriptionName = "sub" },
            eventBus,
            store,
            logger));

        Assert.Throws<ArgumentException>(() => new ServiceBusInboundSubscriber(
            client,
            new ServiceBusSettings { TopicName = "topic" },
            eventBus,
            store,
            logger));
    }

    [Fact]
    public void ServiceBusSettings_StoresConfiguredValues()
    {
        var settings = new ServiceBusSettings
        {
            ConnectionString = "Endpoint=sb://queue-test/",
            TopicName = "topic",
            SubscriptionName = "sub"
        };

        Assert.Equal("Endpoint=sb://queue-test/", settings.ConnectionString);
        Assert.Equal("topic", settings.TopicName);
        Assert.Equal("sub", settings.SubscriptionName);
    }
}
