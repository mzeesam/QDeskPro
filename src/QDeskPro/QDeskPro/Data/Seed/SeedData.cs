using Microsoft.AspNetCore.Identity;
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

        // Seed roles
        await SeedRolesAsync(roleManager);

        // Seed products
        await SeedProductsAsync(context);

        // Seed default admin user
        await SeedAdminUserAsync(userManager);

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
}
