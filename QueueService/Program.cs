using QueueService.Infrastructure;
using QueueService.Services;
using QueueService.Events;
using QueueService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RedisConnection>();
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton<IEventPublisher, ConsoleEventPublisher>();
builder.Services.AddScoped<IQueueService, QueueService.Services.QueueService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Subscribe to appointment_checked_in
var bus = app.Services.GetRequiredService<IEventBus>();
var queueService = app.Services.GetRequiredService<IQueueService>();

bus.Subscribe<AppointmentCheckedInEvent>(async evt =>
{
    await queueService.CreateTicket(
        evt.TenantId,
        evt.ServiceId,
        evt.AppointmentId,
        evt.PriorityLevel);
});

app.Run();
