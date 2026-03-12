using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using QueueService.Repositories;
using QueueService.Infrastructure;
using QueueService.Services;
using QueueService.Events;

var builder = WebApplication.CreateBuilder(args);

// ==========================
// INFRASTRUCTURE
// ==========================

builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<ConsoleEventPublisher>();
builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<ConsoleEventPublisher>());

// Optional: Azure Service Bus integration (safe when not configured)
var sbConn = ServiceBusConfig.GetConnectionString(builder.Configuration);
var sbTopic = ServiceBusConfig.GetTopic(builder.Configuration);
var sbSubscription = ServiceBusConfig.GetSubscription(builder.Configuration);

if (!string.IsNullOrWhiteSpace(sbConn) && !string.IsNullOrWhiteSpace(sbTopic))
{
    try
    {
        _ = ServiceBusConnectionStringProperties.Parse(sbConn);

        builder.Services.AddSingleton(_ => new ServiceBusClient(sbConn));
        builder.Services.AddSingleton<ServiceBusEventPublisher>();
        builder.Services.AddSingleton<IEventPublisher>(sp =>
            new TeeEventPublisher(
                sp.GetRequiredService<ConsoleEventPublisher>(),
                sp.GetRequiredService<ServiceBusEventPublisher>(),
                sp.GetRequiredService<ILogger<TeeEventPublisher>>()));

        if (!string.IsNullOrWhiteSpace(sbSubscription))
            builder.Services.AddHostedService<AppointmentCheckedInServiceBusConsumer>();
    }
    catch
    {
        // Invalid connection string; keep defaults (in-memory + console) without crashing the service.
    }
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
builder.Services.AddScoped<IRedisQueueService, RedisQueueService>();
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
