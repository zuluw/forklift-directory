using ForkliftDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace ForkliftDirectory.Data;

public class AppDbContext : DbContext
{
    static AppDbContext()
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Forklift> Forklifts => Set<Forklift>();

    public DbSet<Downtime> Downtimes => Set<Downtime>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Forklift>(entity =>
        {
            entity.ToTable("Forklifts");

            entity.HasKey(f => f.Id);
            entity.Property(f => f.Id)
                .UseIdentityByDefaultColumn();

            entity.Property(f => f.Brand)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(f => f.Number)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(f => f.Number);

            entity.Property(f => f.Capacity)
                .IsRequired()
                .HasPrecision(10, 3);

            entity.Property(f => f.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(f => f.UpdatedAt)
                .IsRequired();

            entity.Property(f => f.UpdatedBy)
                .IsRequired()
                .HasMaxLength(150);
        });

        modelBuilder.Entity<Downtime>(entity =>
        {
            entity.ToTable("Downtimes");

            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id)
                .UseIdentityByDefaultColumn();

            entity.Property(d => d.ForkliftId)
                .IsRequired();

            entity.Property(d => d.StartTime)
                .IsRequired();

            entity.Property(d => d.EndTime)
                .IsRequired(false);

            entity.Property(d => d.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.HasIndex(d => new { d.ForkliftId, d.StartTime });

            entity.HasOne(d => d.Forklift)
                .WithMany(f => f.Downtimes)
                .HasForeignKey(d => d.ForkliftId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}