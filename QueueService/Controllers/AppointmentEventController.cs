using Microsoft.AspNetCore.Mvc;
using QueueService.Events;      
using QueueService.Models;     

namespace QueueService.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentEventController : ControllerBase
{
    private readonly IEventBus _bus;

    public AppointmentEventController(IEventBus bus)
    {
        _bus = bus;
    }

    [HttpPost("check-in")]
    public async Task<IActionResult> CheckIn(AppointmentCheckedInEvent evt)
    {
        await _bus.Publish(evt);
        return Ok("appointment_checked_in published");
    }
}
