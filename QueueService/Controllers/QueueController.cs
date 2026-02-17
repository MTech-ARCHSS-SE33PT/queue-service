using Microsoft.AspNetCore.Mvc;
using QueueService.Services;    
using QueueService.Models;      

namespace QueueService.Controllers;

[ApiController]
[Route("api/queue")]
public class QueueController : ControllerBase
{
    private readonly IQueueService _service;

    public QueueController(IQueueService service)
    {
        _service = service;
    }

    [HttpPost("configure")]
    public Task Configure(Guid tenantId, Guid serviceId, int maxCounters)
        => _service.Configure(tenantId, serviceId, maxCounters);

    [HttpPost("ticket")]
    public Task<QueueEntry> Create(Guid tenantId, Guid serviceId, Guid? appointmentId, int priority)
        => _service.CreateTicket(tenantId, serviceId, appointmentId, priority);

    [HttpPost("call-next")]
    public Task<QueueEntry?> Call(Guid tenantId, Guid serviceId, string counterId)
        => _service.CallNext(tenantId, serviceId, counterId);

    [HttpPost("complete")]
    public Task Complete(Guid queueEntryId)
        => _service.CompleteTicket(queueEntryId);


    [HttpGet("status")]
    public Task<object> Status(Guid tenantId, Guid serviceId)
        => _service.GetStatus(tenantId, serviceId);
}
