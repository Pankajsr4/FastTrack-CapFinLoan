using CapFinLoan.Notification.Application.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapFinLoan.Notification.Infrastructure.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notif");

        modelBuilder.Entity<NotificationRecord>(e =>
        {
            e.ToTable("Notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.ApplicationNumber).HasMaxLength(50);
            e.Property(x => x.Type).HasMaxLength(30).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
