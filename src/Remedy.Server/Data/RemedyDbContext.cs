using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Models;

namespace Remedy.Server.Data;

public class RemedyDbContext : DbContext
{
    public DbSet<Resource> Resources { get; set; }
    
    public DbSet<TimeSlot> TimeSlots { get; set; }

    public string DbPath { get; }

    public RemedyDbContext()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var remedyFolder = Path.Combine(folder, "Remedy");
        Directory.CreateDirectory(remedyFolder);
        DbPath = Path.Combine(remedyFolder, "remedy.db");
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) => 
        options.UseSqlite($"Data Source={DbPath}");

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

            entity.HasOne(e => e.PreferredTimeSlot)
                .WithMany(t => t.Resources)
                .HasForeignKey(e => e.PreferredTimeSlotId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.Ignore(e => e.ComputedScore);
        });

        // Configure TimeSlot
        modelBuilder.Entity<TimeSlot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });
    }
}
