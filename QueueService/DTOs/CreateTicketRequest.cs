namespace QueueService.DTOs;

public class CreateTicketRequest
{
    public Guid TenantId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid? AppointmentId { get; set; }
    public int Priority { get; set; }
}