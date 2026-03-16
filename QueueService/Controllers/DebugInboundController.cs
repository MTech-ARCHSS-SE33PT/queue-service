using Microsoft.AspNetCore.Mvc;
using QueueService.Infrastructure.ServiceBus;

namespace QueueService.Controllers;

[ApiController]
[Route("api/debug/inbound")]
public sealed class DebugInboundController : ControllerBase
{
    [HttpGet]
    public IActionResult Get([FromServices] InboundMessageStore store)
        => Ok(new { ok = true, items = store.Snapshot() });
}

