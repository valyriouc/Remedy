using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Models;

namespace Remedy.Shared.Data;

public class RemedyDbContext : DbContext
{
    public DbSet<Resource> Resources { get; set; }
    public DbSet<TimeSlot> TimeSlots { get; set; }

    public string DbPath { get; }

    public RemedyDbContext(string? databaseName=null)
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var remedyFolder = Path.Combine(folder, "Remedy");
        Directory.CreateDirectory(remedyFolder);
        DbPath = Path.Combine(remedyFolder, databaseName ?? "sync.db");
    }

    public RemedyDbContext(DbContextOptions<RemedyDbContext> options, string? databaseName=null) : base(options)
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var remedyFolder = Path.Combine(folder, "Remedy");
        Directory.CreateDirectory(remedyFolder);
        DbPath = Path.Combine(remedyFolder, databaseName ?? "sync.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Resource
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Priority).HasDefaultValue(1.0);
            entity.Property(e => e.RelevanceScore).HasDefaultValue(1.0);
            entity.Property(e => e.SavedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

            // Sync-related fields
            entity.Property(e => e.SyncStatus).HasDefaultValue(SyncStatus.LocalOnly);
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.SyncRetryCount).HasDefaultValue(0);

            entity.HasOne(e => e.PreferredTimeSlot)
                .WithMany(t => t.Resources)
                .HasForeignKey(e => e.PreferredTimeSlotId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Ignore(e => e.ComputedScore);

            // Index for sync queries
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => new { e.SyncStatus, e.ModifiedAt });
        });

        // Configure TimeSlot
        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();

            // Sync-related fields
            entity.Property(e => e.SyncStatus).HasDefaultValue(SyncStatus.LocalOnly);
            entity.Property(e => e.ModifiedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.Version).HasDefaultValue(1);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.SyncRetryCount).HasDefaultValue(0);

            // Index for sync queries
            entity.HasIndex(e => e.SyncStatus);
            entity.HasIndex(e => new { e.SyncStatus, e.ModifiedAt });
        });
    }
}
