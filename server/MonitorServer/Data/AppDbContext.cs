using Microsoft.EntityFrameworkCore;
using MonitorServer.Models;

namespace MonitorServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<EventLog> EventLogs => Set<EventLog>();
    public DbSet<Segment> Segments => Set<Segment>();
    public DbSet<AppUsage> AppUsages => Set<AppUsage>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>().HasIndex(d => d.Code).IsUnique();
        modelBuilder.Entity<Device>().HasIndex(d => d.HostName);
        modelBuilder.Entity<AdminUser>().HasIndex(a => a.Username).IsUnique();

        modelBuilder.Entity<EventLog>()
            .HasOne(e => e.Device).WithMany(d => d.Events).HasForeignKey(e => e.DeviceId);
        modelBuilder.Entity<Segment>()
            .HasOne(s => s.Device).WithMany(d => d.Segments).HasForeignKey(s => s.DeviceId);
        modelBuilder.Entity<AppUsage>()
            .HasOne(a => a.Device).WithMany(d => d.AppUsages).HasForeignKey(a => a.DeviceId);
        modelBuilder.Entity<DailySummary>()
            .HasOne(s => s.Device).WithMany().HasForeignKey(s => s.DeviceId);
        modelBuilder.Entity<DailySummary>()
            .HasIndex(s => new { s.DeviceId, s.Date }).IsUnique();
    }
}
