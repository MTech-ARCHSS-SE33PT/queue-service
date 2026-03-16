using System.Collections.Concurrent;

namespace QueueService.Infrastructure.ServiceBus;

public sealed class InboundMessageStore
{
    public sealed record Item(
        string? Subject,
        string? MessageId,
        DateTimeOffset ReceivedAtUtc,
        Guid? TenantId = null,
        Guid? ServiceId = null,
        Guid? AppointmentId = null);

    private readonly ConcurrentQueue<Item> _items = new();

    public void Record(string? subject, string? messageId, DateTimeOffset receivedAtUtc)
    {
        _items.Enqueue(new Item(subject, messageId, receivedAtUtc));
        Trim();
    }

    public void RecordAppointmentCheckedIn(
        string? subject,
        string? messageId,
        DateTimeOffset receivedAtUtc,
        Guid tenantId,
        Guid serviceId,
        Guid appointmentId)
    {
        _items.Enqueue(new Item(subject, messageId, receivedAtUtc, tenantId, serviceId, appointmentId));
        Trim();
    }

    private void Trim()
    {
        while (_items.Count > 50 && _items.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<Item> Snapshot()
        => _items.ToArray();
}
