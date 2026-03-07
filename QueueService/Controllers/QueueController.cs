using Microsoft.AspNetCore.Mvc;
using QueueService.Services;
using QueueService.DTOs;

namespace QueueService.Controllers;

[ApiController]
[Route("api/queue")]
public class QueueController : ControllerBase
{
    private readonly QueueOrchestratorService _orchestrator;

    public QueueController(QueueOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [HttpPost("configure")]
    public async Task<IActionResult> Configure(
        Guid tenantId,
        Guid serviceId,
        string serviceName,
        string locationName,
        int maxCounters)
    {
        await _orchestrator.Configure(
            tenantId, serviceId, serviceName, locationName, maxCounters);

        return Ok();
    }

    [HttpPost("ticket")]
    public async Task<ActionResult<QueueEntryDto>> Create(
        Guid tenantId,
        Guid serviceId,
        Guid? appointmentId,
        int priority)
    {
        var result = await _orchestrator.CreateTicket(
            tenantId, serviceId, appointmentId, priority);

        return Ok(result);
    }

    [HttpPost("call-next")]
    public async Task<ActionResult<QueueEntryDto?>> Call(
        Guid tenantId,
        Guid serviceId,
        string counterId)
    {
        var result = await _orchestrator.CallNext(
            tenantId, serviceId, counterId);

        return Ok(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(Guid queueEntryId)
    {
        await _orchestrator.CompleteTicket(queueEntryId);
        return Ok();
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(Guid tenantId, Guid serviceId)
    {
        var result = await _orchestrator.GetStatus(tenantId, serviceId);
        return Ok(result);
    }

    [HttpPut]
public async Task<IActionResult> Update(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId,
    [FromQuery] int maxCounters)
{
    if (maxCounters < 1)
        return BadRequest("MaxCounters must be at least 1.");

    await _orchestrator.Update(tenantId, serviceId, maxCounters);

    return Ok("Queue updated successfully.");
}

[HttpDelete]
public async Task<IActionResult> Delete(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId)
{
    await _orchestrator.Delete(tenantId, serviceId);

    return Ok("Queue deleted successfully.");
}

    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetQueuesByTenant(Guid tenantId)
    {
        var result = await _orchestrator.GetQueuesByTenantAsync(tenantId);
        return Ok(result);
    }
}