using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using QueueService.Repositories;
using QueueService.Infrastructure;
using QueueService.Infrastructure.ServiceBus;
using QueueService.Services;
using QueueService.Events;

DotEnv.LoadFromWellKnownLocations();

var builder = WebApplication.CreateBuilder(args);

// ==========================
// INFRASTRUCTURE
// ==========================

builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<InboundMessageStore>();

static string? FirstNonEmpty(params string?[] values)
{
    foreach (var v in values)
    {
        if (!string.IsNullOrWhiteSpace(v))
            return v;
    }

    return null;
}

var asbConn =
    FirstNonEmpty(
        builder.Configuration["ASB_CONN"],
        builder.Configuration["ServiceBus:ConnectionString"],
        builder.Configuration.GetConnectionString("ServiceBus"));

if (string.IsNullOrWhiteSpace(asbConn) &&
    DotEnv.TryGetValueFromWellKnownLocations("ASB_CONN", out var asbConnFromFile) &&
    !string.IsNullOrWhiteSpace(asbConnFromFile))
{
    asbConn = asbConnFromFile;
}

var asbTopic =
    FirstNonEmpty(
        builder.Configuration["ASB_TOPIC"],
        builder.Configuration["ServiceBus:TopicName"]);

if (string.IsNullOrWhiteSpace(asbTopic) &&
    DotEnv.TryGetValueFromWellKnownLocations("ASB_TOPIC", out var asbTopicFromFile) &&
    !string.IsNullOrWhiteSpace(asbTopicFromFile))
{
    asbTopic = asbTopicFromFile;
}

var asbSub =
    FirstNonEmpty(
        builder.Configuration["ASB_SUB"],
        builder.Configuration["ServiceBus:SubscriptionName"]);

if (string.IsNullOrWhiteSpace(asbSub) &&
    DotEnv.TryGetValueFromWellKnownLocations("ASB_SUB", out var asbSubFromFile) &&
    !string.IsNullOrWhiteSpace(asbSubFromFile))
{
    asbSub = asbSubFromFile;
}

var serviceBusSettings = new ServiceBusSettings
{
    ConnectionString = asbConn,
    TopicName = asbTopic,
    SubscriptionName = asbSub,
};

if (!string.IsNullOrWhiteSpace(serviceBusSettings.ConnectionString) &&
    !string.IsNullOrWhiteSpace(serviceBusSettings.TopicName))
{
    builder.Services.AddSingleton(serviceBusSettings);
    builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusSettings.ConnectionString));
    builder.Services.AddSingleton<IEventPublisher>(sp =>
        new ServiceBusEventPublisher(
            sp.GetRequiredService<ServiceBusClient>(),
            serviceBusSettings.TopicName));

    if (!string.IsNullOrWhiteSpace(serviceBusSettings.SubscriptionName))
        builder.Services.AddHostedService<ServiceBusInboundSubscriber>();
}
else
{
    builder.Services.AddSingleton<IEventPublisher, ConsoleEventPublisher>();
}

// ==========================
// DATABASE
// ==========================

builder.Services.AddDbContext<QueueDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("QueueDb")));

// ==========================
// APPLICATION SERVICES
// ==========================

builder.Services.AddScoped<IQueueRepository, SqlQueueRepository>();
var redisConnString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnString))
{
    builder.Services.AddSingleton<RedisConnection>();
    builder.Services.AddScoped<IRedisQueueService, RedisQueueService>();
}
else
{
    builder.Services.AddSingleton<IRedisQueueService, InMemoryRedisQueueService>();
}
builder.Services.AddScoped<QueueOrchestratorService>();

// ==========================
// API
// ==========================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting(); 
app.UseCors("AllowFrontend");
app.MapControllers();

// ==========================
// EVENT SUBSCRIPTION
// ==========================

var bus = app.Services.GetRequiredService<IEventBus>();

bus.Subscribe<AppointmentCheckedInEvent>(async evt =>
{
    using var scope = app.Services.CreateScope();
    var orchestrator = scope.ServiceProvider
        .GetRequiredService<QueueOrchestratorService>();

    await orchestrator.CreateTicket(
        evt.TenantId,
        evt.ServiceId,
        evt.AppointmentId,
        evt.PriorityLevel);
});

app.Run();
