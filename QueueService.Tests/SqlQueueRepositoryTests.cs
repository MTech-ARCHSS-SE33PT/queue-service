using Xunit;
using Microsoft.EntityFrameworkCore;
using QueueService.Repositories;
using QueueService.Infrastructure;
using QueueService.Models;
using QueueService.DTOs;

namespace QueueService.Tests;

public class SqlQueueRepositoryTests : IDisposable
{
    private readonly QueueDbContext _context;
    private readonly SqlQueueRepository _repository;

    public SqlQueueRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<QueueDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new QueueDbContext(options);
        _repository = new SqlQueueRepository(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ConfigureAsync_ShouldCreateNewConfiguration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var serviceName = "Test Service";
        var locationName = "Test Location";
        var maxCounters = 5;

        // Act
        await _repository.ConfigureAsync(tenantId, serviceId, serviceName, locationName, maxCounters);

        // Assert
        var config = await _context.QueueConfigurations
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.ServiceId == serviceId);
        Assert.NotNull(config);
        Assert.Equal(serviceName, config.ServiceName);
        Assert.Equal(locationName, config.LocationName);
        Assert.Equal(maxCounters, config.MaxCounters);
    }

    [Fact]
    public async Task ConfigureAsync_ShouldUpdateExistingConfiguration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var initialConfig = new QueueConfiguration
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ServiceId = serviceId,
            ServiceName = "Old Name",
            LocationName = "Old Location",
            MaxCounters = 1
        };
        _context.QueueConfigurations.Add(initialConfig);
        await _context.SaveChangesAsync();

        // Act
        await _repository.ConfigureAsync(tenantId, serviceId, "New Name", "New Location", 10);

        // Assert
        var config = await _context.QueueConfigurations
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.ServiceId == serviceId);
        Assert.NotNull(config);
        Assert.Equal("New Name", config.ServiceName);
        Assert.Equal("New Location", config.LocationName);
        Assert.Equal(10, config.MaxCounters);
    }

    [Fact]
    public async Task CreateTicketAsync_ShouldCreateTicket()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        var priority = 2;

        // Create configuration first
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);

        // Act
        var ticket = await _repository.CreateTicketAsync(tenantId, serviceId, appointmentId, priority);

        // Assert
        Assert.NotNull(ticket);
        Assert.Equal(tenantId, ticket.TenantId);
        Assert.Equal(serviceId, ticket.ServiceId);
        Assert.Equal(appointmentId, ticket.AppointmentId);
        Assert.Equal(priority, ticket.PriorityLevel);
        Assert.Equal("WAITING", ticket.Status);
        Assert.NotNull(ticket.TicketNumber);
    }

    [Fact]
    public async Task CreateTicketAsync_ShouldCreateDefaultConfigurationWhenMissing()
    {
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        var ticket = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);

        var config = await _context.QueueConfigurations
            .SingleOrDefaultAsync(q => q.TenantId == tenantId && q.ServiceId == serviceId);
        Assert.NotNull(config);
        Assert.Equal(config!.Id, ticket.QueueConfigurationId);
        Assert.Equal("Auto", config.LocationName);
    }

    [Fact]
    public async Task MarkAsCalledAsync_ShouldUpdateStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);
        var ticket = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        var counterId = "Counter1";

        // Act
        var updatedTicket = await _repository.MarkAsCalledAsync(ticket.Id, counterId);

        // Assert
        Assert.Equal("CALLED", updatedTicket.Status);
        Assert.Equal(counterId, updatedTicket.CounterId);
        Assert.NotNull(updatedTicket.CalledAt);
    }

    [Fact]
    public async Task GetServingTicketsAsync_ShouldReturnCalledTickets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);
        var ticket1 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        var ticket2 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        await _repository.MarkAsCalledAsync(ticket1.Id, "Counter1");

        // Act
        var serving = await _repository.GetServingTicketsAsync(tenantId, serviceId);

        // Assert
        Assert.Single(serving);
        Assert.Equal(ticket1.Id, serving[0].Id);
    }

    [Fact]
    public async Task MarkAsCompletedAsync_ShouldUpdateStatus()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);
        var ticket = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        await _repository.MarkAsCalledAsync(ticket.Id, "Counter1");

        // Act
        var completedTicket = await _repository.MarkAsCompletedAsync(ticket.Id);

        // Assert
        Assert.Equal("COMPLETED", completedTicket.Status);
        Assert.NotNull(completedTicket.ServedAt);
    }

    [Fact]
    public async Task GetNextWaitingTicketAsync_ShouldReturnOldestWaiting()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);
        var ticket1 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        await Task.Delay(10); // Ensure different timestamps
        var ticket2 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);

        // Act
        var next = await _repository.GetNextWaitingTicketAsync(tenantId, serviceId);

        // Assert
        Assert.NotNull(next);
        Assert.Equal(ticket1.Id, next.Id);
    }

    [Fact]
    public async Task GetQueuesByTenantAsync_ShouldReturnConfigurations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId1 = Guid.NewGuid();
        var serviceId2 = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId1, "Service1", "Location1", 5);
        await _repository.ConfigureAsync(tenantId, serviceId2, "Service2", "Location2", 3);

        // Act
        var queues = await _repository.GetQueuesByTenantAsync(tenantId);

        // Assert
        Assert.Equal(2, queues.Count);
        Assert.Contains(queues, q => q.ServiceId == serviceId1);
        Assert.Contains(queues, q => q.ServiceId == serviceId2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMaxCounters()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);

        // Act
        await _repository.UpdateAsync(tenantId, serviceId, 10);

        // Assert
        var config = await _context.QueueConfigurations
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.ServiceId == serviceId);
        Assert.NotNull(config);
        Assert.Equal(10, config.MaxCounters);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowWhenQueueNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _repository.UpdateAsync(tenantId, serviceId, 10));
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveConfiguration()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);

        // Act
        await _repository.DeleteAsync(tenantId, serviceId);

        // Assert
        var config = await _context.QueueConfigurations
            .FirstOrDefaultAsync(q => q.TenantId == tenantId && q.ServiceId == serviceId);
        Assert.Null(config);
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowWhenQueueNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _repository.DeleteAsync(tenantId, serviceId));
    }

    [Fact]
    public async Task GetTicketsByTenantAsync_ShouldReturnOrderedTickets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", 5);
        
        var ticket1 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        await Task.Delay(10); // Ensure different timestamps
        var ticket2 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);

        // Act
        var tickets = await _repository.GetTicketsByTenantAsync(tenantId);

        // Assert
        Assert.Equal(2, tickets.Count);
        // Should be ordered by CreatedAt descending (most recent first)
        Assert.Equal(ticket2.Id, tickets[0].Id);
        Assert.Equal(ticket1.Id, tickets[1].Id);
    }

    [Fact]
    public async Task GetMaxCountersAsync_ShouldReturnMaxCounters()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var maxCounters = 7;
        await _repository.ConfigureAsync(tenantId, serviceId, "Service", "Location", maxCounters);

        // Act
        var result = await _repository.GetMaxCountersAsync(tenantId, serviceId);

        // Assert
        Assert.Equal(maxCounters, result);
    }

    [Fact]
    public async Task GetMaxCountersAsync_ShouldReturnNullWhenNotFound()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();

        // Act
        var result = await _repository.GetMaxCountersAsync(tenantId, serviceId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetStaffTicketsAsync_ShouldReturnMappedStaffTickets()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await _repository.ConfigureAsync(tenantId, serviceId, "Test Service", "Location", 5);
        
        var ticket1 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 1);
        var ticket2 = await _repository.CreateTicketAsync(tenantId, serviceId, null, 2);
        await _repository.MarkAsCalledAsync(ticket1.Id, "Counter1");

        // Act
        var staffTickets = await _repository.GetStaffTicketsAsync(tenantId, serviceId);

        // Assert
        Assert.Equal(2, staffTickets.Count);
        var waitingTicket = staffTickets.First(t => t.Status == "WAITING");
        var calledTicket = staffTickets.First(t => t.Status == "CALLED");
        
        Assert.Equal("Test Service", waitingTicket.ServiceName);
        Assert.Equal("Test Service", calledTicket.ServiceName);
        Assert.Equal("Counter1", calledTicket.CounterId);
    }
}
