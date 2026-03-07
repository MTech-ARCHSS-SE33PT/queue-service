using Microsoft.EntityFrameworkCore;
using QueueService.Infrastructure;
using QueueService.Models;

namespace QueueService.Repositories;

public class SqlQueueRepository : IQueueRepository
{
    private readonly QueueDbContext _context;

    public SqlQueueRepository(QueueDbContext context)
    {
        _context = context;
    }

    public async Task<List<QueueConfiguration>> GetQueuesByTenantAsync(Guid tenantId)
    {
        return await _context.QueueConfigurations
            .Where(q => q.TenantId == tenantId)
            .Include(q => q.QueueEntries)
            .ToListAsync();
    }

    public async Task<QueueEntry> CreateTicketAsync(
        Guid tenantId,
        Guid serviceId,
        Guid? appointmentId,
        int priority)
    {
        var ticket = new QueueEntry
        {
            TenantId = tenantId,
            ServiceId = serviceId,
            AppointmentId = appointmentId,
            PriorityLevel = priority,
            TicketNumber = $"T-{DateTime.UtcNow.Ticks % 10000}"
        };

        _context.QueueEntries.Add(ticket);
        await _context.SaveChangesAsync();

        return ticket;
    }

    public async Task<QueueEntry> MarkAsCalledAsync(
        Guid queueEntryId,
        string counterId)
    {
        var ticket = await _context.QueueEntries
            .FirstAsync(x => x.Id == queueEntryId);

        ticket.Status = "CALLED";
        ticket.CounterId = counterId;
        ticket.CalledAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ticket;
    }

    public async Task<List<QueueEntry>> GetServingTicketsAsync(
        Guid tenantId,
        Guid serviceId)
    {
        return await _context.QueueEntries
            .Where(x =>
                x.TenantId == tenantId &&
                x.ServiceId == serviceId &&
                x.Status == "CALLED")
            .ToListAsync();
    }

    public async Task ConfigureAsync(
    Guid tenantId,
    Guid serviceId,
    string serviceName,
    string locationName,
    int maxCounters)
{
    var existing = await _context.QueueConfigurations
        .FirstOrDefaultAsync(q =>
            q.TenantId == tenantId &&
            q.ServiceId == serviceId);

    if (existing == null)
    {
        var config = new QueueConfiguration
        {
            TenantId = tenantId,
            ServiceId = serviceId,
            ServiceName = serviceName,
            LocationName = locationName,
            MaxCounters = maxCounters
        };

        _context.QueueConfigurations.Add(config);
    }
    else
    {
        existing.ServiceName = serviceName;
        existing.LocationName = locationName;
        existing.MaxCounters = maxCounters;
    }

    await _context.SaveChangesAsync();
}

    public async Task<QueueEntry> MarkAsCompletedAsync(Guid queueEntryId)
    {
        var ticket = await _context.QueueEntries
            .FirstAsync(x => x.Id == queueEntryId);

        ticket.Status = "COMPLETED";
        ticket.ServedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return ticket;
    }

public async Task UpdateAsync(Guid tenantId, Guid serviceId, int maxCounters)
{
    var queue = await _context.QueueConfigurations
        .SingleOrDefaultAsync(q =>
            q.TenantId == tenantId &&
            q.ServiceId == serviceId);

    if (queue == null)
        throw new Exception("Queue not found.");

    queue.MaxCounters = maxCounters;

    await _context.SaveChangesAsync();
}

public async Task DeleteAsync(Guid tenantId, Guid serviceId)
{
    var queue = await _context.QueueConfigurations
        .SingleOrDefaultAsync(q =>
            q.TenantId == tenantId &&
            q.ServiceId == serviceId);

    if (queue == null)
        throw new Exception("Queue not found.");

    _context.QueueConfigurations.Remove(queue);

    await _context.SaveChangesAsync();
}

}