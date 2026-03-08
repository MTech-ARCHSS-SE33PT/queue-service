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
builder.Services.AddSingleton<IEventPublisher, ConsoleEventPublisher>();

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