using QueueService.Models;

namespace QueueService.Services;

public interface IQueueService
{
    Task Configure(Guid tenantId, Guid serviceId, int maxCounters);
    Task<QueueEntry> CreateTicket(Guid tenantId, Guid serviceId, Guid? appointmentId, int priority);
    Task<QueueEntry?> CallNext(Guid tenantId, Guid serviceId, string counterId);
    Task CompleteTicket(Guid queueEntryId);
    Task<object> GetStatus(Guid tenantId, Guid serviceId);
}
