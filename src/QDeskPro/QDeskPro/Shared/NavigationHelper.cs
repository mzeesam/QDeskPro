using MudBlazor;

namespace QDeskPro.Shared;

/// <summary>
/// Navigation item model for role-based navigation.
/// </summary>
public record NavItem(string Href, string Title, string Icon);

/// <summary>
/// Helper class for role-based navigation items.
/// </summary>
public static class NavigationHelper
{
    /// <summary>
    /// Gets navigation items based on user role.
    /// </summary>
    public static List<NavItem> GetNavigationItems(string? role)
    {
        return role switch
        {
            "Administrator" => new List<NavItem>
            {
                new("/admin/managers", "Managers", Icons.Material.Filled.SupervisorAccount),
                new("/admin/quarries", "All Quarries", Icons.Material.Filled.Terrain),
                new("/reports", "Reports", Icons.Material.Filled.Assessment),
            },
            "Manager" => new List<NavItem>
            {
                new("/dashboard", "Analytics", Icons.Material.Filled.Dashboard),
                new("/quarries", "My Quarries", Icons.Material.Filled.Terrain),
                new("/users", "Users", Icons.Material.Filled.People),
                new("/reports", "Reports", Icons.Material.Filled.Assessment),
                new("/master-data", "Master Data", Icons.Material.Filled.Settings),
            },
            "Clerk" => new List<NavItem>
            {
                new("/clerk/dashboard", "Dashboard", Icons.Material.Filled.Dashboard),
                new("/clerk/sales/new", "New Sale", Icons.Material.Filled.AddShoppingCart),
                new("/clerk/expenses", "Expenses", Icons.Material.Filled.Receipt),
                new("/clerk/banking", "Banking", Icons.Material.Filled.AccountBalance),
                new("/clerk/fuel", "Fuel Usage", Icons.Material.Filled.LocalGasStation),
                new("/clerk/reports", "Reports", Icons.Material.Filled.Assessment),
            },
            _ => new List<NavItem>()
        };
    }
}
