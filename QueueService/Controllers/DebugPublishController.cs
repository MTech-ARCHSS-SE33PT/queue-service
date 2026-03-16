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
}

