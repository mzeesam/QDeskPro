using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Domain.Entities;

namespace QDeskPro.Data.Seed;

/// <summary>
/// Seed data for initial database setup.
/// </summary>
public static class SeedData
{
    /// <summary>
    /// Default roles for the application.
    /// </summary>
    public static readonly string[] DefaultRoles = ["Administrator", "Manager", "Clerk"];

    /// <summary>
    /// Default product names.
    /// </summary>
    public static readonly string[] DefaultProducts =
    [
        "Size 6",      // Standard ballast size
        "Size 9",      // Medium ballast
        "Size 4",      // Smaller ballast
        "Reject",      // Rejected/irregular pieces (different fee structure)
        "Hardcore",    // Larger building material
        "Beam"         // Structural pieces
    ];

    /// <summary>
    /// Seeds the database with initial data.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        // Seed roles
        await SeedRolesAsync(roleManager);

        // Seed products
        await SeedProductsAsync(context);

        // Seed default admin user
        await SeedAdminUserAsync(userManager);

        // Seed sample development data (only in Development environment)
        if (env.IsDevelopment())
        {
            await SeedDevelopmentDataAsync(context, userManager);
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in DefaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }
    }

    private static async Task SeedProductsAsync(AppDbContext context)
    {
        if (context.Products.Any())
            return;

        foreach (var productName in DefaultProducts)
        {
            context.Products.Add(new Product
            {
                Id = Guid.NewGuid().ToString(),
                ProductName = productName,
                Description = $"{productName} quarry product",
                IsActive = true,
                DateCreated = DateTime.UtcNow
            });
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
    {
        const string adminEmail = "admin@qdeskpro.com";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin != null)
            return;

        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "System Administrator",
            Position = "Administrator",
            IsActive = true
        };

        var result = await userManager.CreateAsync(adminUser, "Admin@123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Administrator");
        }
    }

    /// <summary>
    /// Seeds sample development data: manager, quarry, clerk, layers, brokers, and prices.
    /// Only runs in Development environment.
    /// </summary>
    private static async Task SeedDevelopmentDataAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        // Check if development data already exists
        if (await context.Quarries.AnyAsync())
            return;

        // 1. Create Manager user
        const string managerEmail = "manager@qdeskpro.com";
        var existingManager = await userManager.FindByEmailAsync(managerEmail);

        if (existingManager == null)
        {
            var managerUser = new ApplicationUser
            {
                UserName = managerEmail,
                Email = managerEmail,
                EmailConfirmed = true,
                FullName = "John Manager",
                Position = "Quarry Manager",
                IsActive = true
            };

            var result = await userManager.CreateAsync(managerUser, "Manager@123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(managerUser, "Manager");
                existingManager = managerUser;
            }
        }

        if (existingManager == null)
            return;

        // 2. Create Quarry (owned by manager)
        var quarry = new Quarry
        {
            Id = Guid.NewGuid().ToString(),
            QuarryName = "Thika - Komu",
            Location = "Thika, Kiambu County",
            ManagerId = existingManager.Id,
            LoadersFee = 50,      // KES 50 per unit
            LandRateFee = 10,     // KES 10 per unit
            RejectsFee = 5,       // KES 5 per unit for Reject products
            EmailRecipients = "reports@qdeskpro.com",
            DailyReportEnabled = true,
            DailyReportTime = new TimeSpan(18, 0, 0), // 6 PM
            IsActive = true,
            DateCreated = DateTime.UtcNow
        };
        context.Quarries.Add(quarry);

        // 3. Create Clerk user assigned to quarry
        const string clerkEmail = "clerk@qdeskpro.com";
        var existingClerk = await userManager.FindByEmailAsync(clerkEmail);

        if (existingClerk == null)
        {
            var clerkUser = new ApplicationUser
            {
                UserName = clerkEmail,
                Email = clerkEmail,
                EmailConfirmed = true,
                FullName = "Jane Clerk",
                Position = "Sales Clerk",
                QuarryId = quarry.Id,
                IsActive = true
            };

            var result = await userManager.CreateAsync(clerkUser, "Clerk@123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(clerkUser, "Clerk");
                existingClerk = clerkUser;
            }
        }

        // 4. Assign clerk to quarry via UserQuarry
        if (existingClerk != null)
        {
            context.UserQuarries.Add(new UserQuarry
            {
                Id = Guid.NewGuid().ToString(),
                UserId = existingClerk.Id,
                QuarryId = quarry.Id,
                IsPrimary = true,
                IsActive = true,
                DateCreated = DateTime.UtcNow
            });
        }

        // 5. Create Layers for the quarry
        var layers = new[]
        {
            new Layer
            {
                Id = Guid.NewGuid().ToString(),
                LayerLevel = "Layer -1",
                DateStarted = DateTime.Today.AddMonths(-6),
                QuarryId = quarry.Id,
                IsActive = true,
                DateCreated = DateTime.UtcNow
            },
            new Layer
            {
                Id = Guid.NewGuid().ToString(),
                LayerLevel = "Layer -2",
                DateStarted = DateTime.Today.AddMonths(-3),
                QuarryId = quarry.Id,
                IsActive = true,
                DateCreated = DateTime.UtcNow
            },
            new Layer
            {
                Id = Guid.NewGuid().ToString(),
                LayerLevel = "Layer -3",
                DateStarted = DateTime.Today.AddMonths(-1),
                QuarryId = quarry.Id,
                IsActive = true,
                DateCreated = DateTime.UtcNow
            }
        };
        context.Layers.AddRange(layers);

        // 6. Create Brokers for the quarry
        var brokers = new[]
        {
            new Broker
            {
                Id = Guid.NewGuid().ToString(),
                BrokerName = "Peter Broker",
                Phone = "0722123456",
                quarryId = quarry.Id,
                IsActive = true,
                DateCreated = DateTime.UtcNow
            },
            new Broker
            {
                Id = Guid.NewGuid().ToString(),
                BrokerName = "Mary Agent",
                Phone = "0733987654",
                quarryId = quarry.Id,
                IsActive = true,
                DateCreated = DateTime.UtcNow
            }
        };
        context.Brokers.AddRange(brokers);

        // 7. Create Product Prices for the quarry
        var products = await context.Products.ToListAsync();
        var prices = new Dictionary<string, double>
        {
            { "Size 6", 18 },
            { "Size 9", 25 },
            { "Size 4", 15 },
            { "Reject", 11 },
            { "Hardcore", 20 },
            { "Beam", 30 }
        };

        foreach (var product in products)
        {
            if (prices.TryGetValue(product.ProductName, out var price))
            {
                context.ProductPrices.Add(new ProductPrice
                {
                    Id = Guid.NewGuid().ToString(),
                    ProductId = product.Id,
                    QuarryId = quarry.Id,
                    Price = price,
                    IsActive = true,
                    DateCreated = DateTime.UtcNow
                });
            }
        }

        await context.SaveChangesAsync();
    }
}
