using Microsoft.EntityFrameworkCore;
using QueueService.Models;

namespace QueueService.Infrastructure;

public class QueueDbContext : DbContext
{
    public QueueDbContext(DbContextOptions<QueueDbContext> options)
        : base(options)
    {
    }

    public DbSet<QueueConfiguration> QueueConfigurations { get; set; }
    public DbSet<QueueEntry> QueueEntries { get; set; }
}