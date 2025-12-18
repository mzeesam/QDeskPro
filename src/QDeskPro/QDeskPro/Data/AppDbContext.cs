using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Domain.Entities;

namespace QDeskPro.Data;

/// <summary>
/// Application database context with all DbSets and entity configurations.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    // DbSets for all domain entities
    public DbSet<Quarry> Quarries { get; set; } = null!;
    public DbSet<Layer> Layers { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductPrice> ProductPrices { get; set; } = null!;
    public DbSet<Broker> Brokers { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<Expense> Expenses { get; set; } = null!;
    public DbSet<Banking> Bankings { get; set; } = null!;
    public DbSet<FuelUsage> FuelUsages { get; set; } = null!;
    public DbSet<DailyNote> DailyNotes { get; set; } = null!;
    public DbSet<UserQuarry> UserQuarries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Quarry configuration
        builder.Entity<Quarry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuarryName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(500);
            
            // Manager relationship (owner)
            entity.HasOne(e => e.Manager)
                .WithMany(u => u.OwnedQuarries)
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ManagerId);
        });

        // Layer configuration
        builder.Entity<Layer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LayerLevel).IsRequired().HasMaxLength(100);
            
            entity.HasOne(e => e.Quarry)
                .WithMany(q => q.Layers)
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.QuarryId);
        });

        // Product configuration
        builder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(100);
        });

        // ProductPrice configuration
        builder.Entity<ProductPrice>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Product)
                .WithMany(p => p.Prices)
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Quarry)
                .WithMany(q => q.ProductPrices)
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.ProductId, e.QuarryId });
        });

        // Broker configuration
        builder.Entity<Broker>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BrokerName).IsRequired().HasMaxLength(200);
            
            entity.HasOne(e => e.Quarry)
                .WithMany(q => q.Brokers)
                .HasForeignKey(e => e.quarryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.quarryId);
        });

        // Sale configuration
        builder.Entity<Sale>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VehicleRegistration).IsRequired().HasMaxLength(20);
            entity.Property(e => e.PaymentStatus).HasMaxLength(20);
            entity.Property(e => e.PaymentMode).HasMaxLength(20);
            
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Layer)
                .WithMany()
                .HasForeignKey(e => e.LayerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Broker)
                .WithMany()
                .HasForeignKey(e => e.BrokerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Clerk)
                .WithMany()
                .HasForeignKey(e => e.ApplicationUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Ignore computed property
            entity.Ignore(e => e.GrossSaleAmount);

            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.ApplicationUserId);
        });

        // Expense configuration
        builder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Item).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(100);

            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.ApplicationUserId);
        });

        // Banking configuration
        builder.Entity<Banking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Item).HasMaxLength(500);
            entity.Property(e => e.RefCode).HasMaxLength(20);

            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.ApplicationUserId);
        });

        // FuelUsage configuration
        builder.Entity<FuelUsage>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Ignore computed properties
            entity.Ignore(e => e.TotalStock);
            entity.Ignore(e => e.Used);
            entity.Ignore(e => e.Balance);

            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.QId);
        });

        // DailyNote configuration
        builder.Entity<DailyNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(1000);

            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.quarryId);
        });

        // UserQuarry configuration
        builder.Entity<UserQuarry>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.QuarryAssignments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Quarry)
                .WithMany(q => q.UserQuarries)
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.QuarryId }).IsUnique();
        });
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically set audit fields.
    /// </summary>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.DateCreated = DateTime.UtcNow;
                    entry.Entity.IsActive = true;
                    break;
                case EntityState.Modified:
                    entry.Entity.DateModified = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
