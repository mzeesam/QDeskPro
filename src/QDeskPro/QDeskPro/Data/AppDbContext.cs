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
    public DbSet<Prepayment> Prepayments { get; set; } = null!;

    // AI-related DbSets
    public DbSet<AIConversation> AIConversations { get; set; } = null!;
    public DbSet<AIMessage> AIMessages { get; set; } = null!;

    // Authentication DbSets
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    // Accounting DbSets
    public DbSet<LedgerAccount> LedgerAccounts { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;
    public DbSet<AccountingPeriod> AccountingPeriods { get; set; } = null!;

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
                .OnDelete(DeleteBehavior.Restrict); // Changed to Restrict to avoid cascade cycles

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
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Quarry)
                .WithMany(q => q.ProductPrices)
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .OnDelete(DeleteBehavior.Restrict);

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

            entity.HasOne(e => e.Prepayment)
                .WithMany(p => p.FulfillmentSales)
                .HasForeignKey(e => e.PrepaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ignore computed property
            entity.Ignore(e => e.GrossSaleAmount);

            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.ApplicationUserId);
            entity.HasIndex(e => e.PrepaymentId);
        });

        // Prepayment configuration
        builder.Entity<Prepayment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VehicleRegistration).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ClientName).HasMaxLength(200);
            entity.Property(e => e.ClientPhone).HasMaxLength(20);
            entity.Property(e => e.PaymentMode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PaymentReference).HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ApplicationUserId).IsRequired();
            entity.Property(e => e.ClerkName).IsRequired().HasMaxLength(200);

            entity.HasOne(e => e.IntendedProduct)
                .WithMany()
                .HasForeignKey(e => e.IntendedProductId)
                .OnDelete(DeleteBehavior.SetNull);

            // Ignore computed property
            entity.Ignore(e => e.RemainingBalance);

            entity.HasIndex(e => e.VehicleRegistration);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.DateStamp);
            entity.HasIndex(e => e.PrepaymentDate);
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
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Quarry)
                .WithMany(q => q.UserQuarries)
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.UserId, e.QuarryId }).IsUnique();
        });

        // AIConversation configuration
        builder.Entity<AIConversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ChatType).HasMaxLength(50);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Quarry)
                .WithMany()
                .HasForeignKey(e => e.QuarryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastMessageAt);
        });

        // AIMessage configuration
        builder.Entity<AIMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ToolName).HasMaxLength(100);

            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.AIConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.AIConversationId);
            entity.HasIndex(e => e.Timestamp);
        });

        // RefreshToken configuration
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(45); // IPv6 max length

            // User relationship
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => new { e.UserId, e.IsRevoked, e.ExpiresAt });
        });

        // ApplicationUser - Manager hierarchy configuration
        builder.Entity<ApplicationUser>(entity =>
        {
            // Self-referential relationship for Manager hierarchy
            // A Manager Owner (null CreatedByManagerId) can create Secondary Managers
            entity.HasOne(e => e.CreatedByManager)
                .WithMany(m => m.CreatedUsers)
                .HasForeignKey(e => e.CreatedByManagerId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading deletes

            entity.HasIndex(e => e.CreatedByManagerId);
        });

        // ===== ACCOUNTING ENTITIES =====

        // LedgerAccount configuration
        builder.Entity<LedgerAccount>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountCode).IsRequired().HasMaxLength(20);
            entity.Property(e => e.AccountName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);

            // Self-referential relationship for parent-child account hierarchy
            entity.HasOne(e => e.ParentAccount)
                .WithMany(e => e.ChildAccounts)
                .HasForeignKey(e => e.ParentAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for efficient querying
            entity.HasIndex(e => e.AccountCode);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => new { e.QId, e.AccountCode }).IsUnique();
        });

        // JournalEntry configuration
        builder.Entity<JournalEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.EntryType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.SourceEntityType).HasMaxLength(50);
            entity.Property(e => e.SourceEntityId).HasMaxLength(50);

            // Ignore computed property
            entity.Ignore(e => e.IsBalanced);

            // Indexes for efficient querying
            entity.HasIndex(e => e.Reference);
            entity.HasIndex(e => e.EntryDate);
            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.FiscalYear);
            entity.HasIndex(e => new { e.FiscalYear, e.FiscalPeriod });
            entity.HasIndex(e => new { e.SourceEntityType, e.SourceEntityId });
        });

        // JournalEntryLine configuration
        builder.Entity<JournalEntryLine>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Memo).HasMaxLength(500);

            entity.HasOne(e => e.JournalEntry)
                .WithMany(j => j.Lines)
                .HasForeignKey(e => e.JournalEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.LedgerAccount)
                .WithMany(a => a.JournalEntryLines)
                .HasForeignKey(e => e.LedgerAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.JournalEntryId);
            entity.HasIndex(e => e.LedgerAccountId);
        });

        // AccountingPeriod configuration
        builder.Entity<AccountingPeriod>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PeriodName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PeriodType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ClosingNotes).HasMaxLength(1000);

            entity.HasIndex(e => e.QId);
            entity.HasIndex(e => e.FiscalYear);
            entity.HasIndex(e => new { e.QId, e.FiscalYear, e.PeriodNumber }).IsUnique();
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
