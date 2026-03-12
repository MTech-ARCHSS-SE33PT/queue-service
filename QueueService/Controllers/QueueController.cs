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
    [FromBody] CreateTicketRequest request)
{
    var result = await _orchestrator.CreateTicket(
        request.TenantId,
        request.ServiceId,
        request.AppointmentId,
        request.Priority);

    return Ok(result);
}

   [HttpPost("call-next")]
public async Task<ActionResult<QueueEntryDto?>> Call(
    Guid tenantId,
    Guid serviceId,
    string counterId)
{
    try
    {
        var result = await _orchestrator.CallNext(
            tenantId, serviceId, counterId);

        if (result == null)
            return NotFound("No waiting tickets.");

        return Ok(result);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
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

    [HttpGet("tickets/{tenantId}")]
public async Task<IActionResult> GetTicketsByTenant(Guid tenantId)
{
    var tickets = await _orchestrator.GetTicketsByTenantAsync(tenantId);
    return Ok(tickets);
}

[HttpGet("maxcounters")]
public async Task<IActionResult> GetMaxCounters(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId)
{
    var maxCounters = await _orchestrator.GetMaxCountersAsync(tenantId, serviceId);

    if (maxCounters == null)
        return NotFound("Queue configuration not found.");

    return Ok(new { MaxCounters = maxCounters });
}
[HttpPost("set-counter")]
public async Task<IActionResult> SetCounter(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId,
    [FromQuery] string userId,
    [FromQuery] string counterNumber)
{
    var result = await _orchestrator.SetCounterAsync(
        tenantId,
        serviceId,
        userId,
        counterNumber);

    if (!result.Success)
        return BadRequest(result.Error);

    return Ok("Counter assigned successfully.");
}
[HttpGet("active-counters")]
public async Task<IActionResult> GetActiveCounters(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId)
{
    var counters = await _orchestrator.GetActiveCountersAsync(
        tenantId, serviceId);

    return Ok(counters);
}

[HttpDelete("release-counter")]
public async Task<IActionResult> ReleaseCounter(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId,
    [FromQuery] string userId)
{
    await _orchestrator.RemoveCounterAsync(
        tenantId,
        serviceId,
        userId);

    return Ok();
}

[HttpDelete("release-counter-if-idle")]
public async Task<IActionResult> ReleaseCounterIfIdle(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId,
    [FromQuery] string userId)
{
    var result = await _orchestrator.ReleaseCounterIfIdleAsync(
        tenantId, serviceId, userId);

    if (!result.Success)
    {
        if (result.Error == "Counter is currently serving a ticket.")
            return Conflict(result.Error);

        return NotFound(result.Error);
    }

    return Ok();
}
[HttpGet("staff-tickets")]
public async Task<IActionResult> GetStaffTickets(
    [FromQuery] Guid tenantId,
    [FromQuery] Guid serviceId)
{
    var result = await _orchestrator.GetStaffTicketsAsync(tenantId, serviceId);
    return Ok(result);
}
}
