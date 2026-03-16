using Microsoft.EntityFrameworkCore;
using QueueService.Models;
using QueueService.DTOs;

namespace QueueService.Repositories;

public interface IQueueRepository
{
    Task<List<QueueConfiguration>> GetQueuesByTenantAsync(Guid tenantId);

    Task UpdateAsync(Guid tenantId, Guid serviceId, int maxCounters);
Task DeleteAsync(Guid tenantId, Guid serviceId);
Task<List<QueueEntry>> GetTicketsByTenantAsync(Guid tenantId);
Task<int?> GetMaxCountersAsync(Guid tenantId, Guid serviceId);
Task<List<StaffTicketDto>> GetStaffTicketsAsync(Guid tenantId, Guid serviceId);
Task<QueueEntry?> GetNextWaitingTicketAsync(
    Guid tenantId,
    Guid serviceId);

    Task<QueueEntry> CreateTicketAsync(
        Guid tenantId,
        Guid serviceId,
        Guid? appointmentId,
        int priority);

    Task<QueueEntry> MarkAsCalledAsync(
        Guid queueEntryId,
        string counterId);

    Task<List<QueueEntry>> GetServingTicketsAsync(
        Guid tenantId,
        Guid serviceId);

    Task ConfigureAsync(Guid tenantId, Guid serviceId,
    string serviceName, string locationName, int maxCounters);

Task<QueueEntry> MarkAsCompletedAsync(Guid queueEntryId);

    Task<QueueEntry?> GetTicketByIdAsync(Guid queueEntryId);
}
