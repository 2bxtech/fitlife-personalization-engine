using FitLife.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace FitLife.Infrastructure.Data;

/// <summary>
/// Main database context for FitLife application
/// </summary>
public class FitLifeDbContext : DbContext
{
    public FitLifeDbContext(DbContextOptions<FitLifeDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.FitnessLevel).HasMaxLength(50);
            entity.Property(e => e.Segment).HasMaxLength(50);
            
            // JSON columns for Goals and PreferredClassTypes
            entity.Property(e => e.Goals).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PreferredClassTypes).HasColumnType("nvarchar(max)");
        });

        // Class configuration
        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Composite index for efficient querying: StartTime + Type + IsActive
            entity.HasIndex(e => new { e.StartTime, e.Type, e.IsActive })
                  .HasFilter("[IsActive] = 1");
            
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.InstructorId).HasMaxLength(100);
            entity.Property(e => e.InstructorName).HasMaxLength(200);
            entity.Property(e => e.Level).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.AverageRating).HasPrecision(3, 2); // e.g., 4.85
        });

        // Interaction configuration (event store)
        modelBuilder.Entity<Interaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Critical index for recent events query
            entity.HasIndex(e => new { e.UserId, e.Timestamp })
                  .IsDescending(false, true); // DESC on Timestamp for recent-first
            
            entity.HasIndex(e => e.ItemId);
            
            entity.Property(e => e.EventType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ItemType).HasMaxLength(50);
            entity.Property(e => e.Metadata).HasColumnType("nvarchar(max)");
            
            // No FK constraint on ItemId (flexible event store)
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Interactions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Recommendation configuration
        modelBuilder.Entity<Recommendation>(entity =>
        {
            // Composite primary key
            entity.HasKey(e => new { e.UserId, e.ItemId });
            
            // Covering index for fast retrieval with score and reason
            entity.HasIndex(e => new { e.UserId, e.Rank })
                  .IncludeProperties(e => new { e.Score, e.Reason });
            
            entity.Property(e => e.ItemType).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Score).HasPrecision(10, 4); // e.g., 98.7543
            
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Recommendations)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Optional navigation to Class (not enforced FK for flexibility)
            entity.HasOne(e => e.Class)
                  .WithMany()
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
