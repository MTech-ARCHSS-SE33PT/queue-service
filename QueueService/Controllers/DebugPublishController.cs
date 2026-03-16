using Microsoft.AspNetCore.Mvc;
using QueueService.Events;

namespace QueueService.Controllers;

[ApiController]
[Route("api/debug/publish")]
public class DebugPublishController : ControllerBase
{
    private readonly IEventPublisher _publisher;

    public DebugPublishController(IEventPublisher publisher)
    {
        _publisher = publisher;
    }

    [HttpPost("ticket-created")]
    public async Task<IActionResult> PublishTicketCreated([FromBody] TicketCreatedEvent? evt)
    {
        evt ??= new TicketCreatedEvent(
            TicketId: Guid.NewGuid(),
            TenantId: Guid.NewGuid(),
            ServiceId: Guid.NewGuid(),
            AppointmentId: Guid.NewGuid(),
            TicketNumber: $"T{Random.Shared.Next(1, 9999):D4}",
            PriorityLevel: 1,
            EnqueuedAt: DateTime.UtcNow);

        await _publisher.PublishAsync("ticket_created", evt);
        return Ok(new { ok = true, published = "ticket_created", payload = evt });
    }

    [HttpPost("ticket-called")]
    public async Task<IActionResult> PublishTicketCalled([FromBody] TicketCalledEvent? evt)
    {
        evt ??= new TicketCalledEvent(
            TicketId: Guid.NewGuid(),
            CounterId: "C1",
            CalledAt: DateTime.UtcNow);

        await _publisher.PublishAsync("ticket_called", evt);
        return Ok(new { ok = true, published = "ticket_called", payload = evt });
    }

    [HttpPost("queue-position-changed")]
    public async Task<IActionResult> PublishQueuePositionChanged([FromBody] QueuePositionChangedEnvelope? envelope)
    {
        envelope ??= new QueuePositionChangedEnvelope(
            EventId: Guid.NewGuid(),
            EventType: "QueuePositionChanged",
            TenantId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow,
            Data: new QueuePositionChangedData(
                TicketNo: $"Q-{Random.Shared.Next(1, 999):D3}",
                Position: Random.Shared.Next(1, 20),
                Customer: new QueueCustomer("Ben", "+6591234567")));

        await _publisher.PublishAsync("QueuePositionChanged", envelope);
        return Ok(new { ok = true, published = "QueuePositionChanged", payload = envelope });
    }
}
