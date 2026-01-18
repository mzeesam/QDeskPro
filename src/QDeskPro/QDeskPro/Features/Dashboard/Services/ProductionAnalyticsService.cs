namespace QDeskPro.Features.Dashboard.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QDeskPro.Data;

/// <summary>
/// Service for production-focused analytics dashboard.
/// Focus areas: quantities, products, layers, destinations, and clients.
/// </summary>
public class ProductionAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ProductionAnalyticsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Get comprehensive production statistics for a date range and quarry.
    /// </summary>
    public async Task<ProductionDashboardData> GetProductionDashboardAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Base query for sales
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        // Filter by quarry if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .ToListAsync();

        // Calculate summary stats
        var totalQuantity = sales.Sum(s => s.Quantity);
        var totalOrders = sales.Count;
        var totalRevenue = sales.Sum(s => s.GrossSaleAmount);
        var uniqueClients = sales.Where(s => !string.IsNullOrEmpty(s.ClientName))
            .Select(s => s.ClientName!.ToLower().Trim())
            .Distinct()
            .Count();
        var uniqueVehicles = sales.Select(s => s.VehicleRegistration.ToUpper().Trim()).Distinct().Count();

        // Production by Product
        var productionByProduct = sales
            .GroupBy(s => s.Product?.ProductName ?? "Unknown")
            .Select(g => new ProductionByCategory
            {
                Category = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count(),
                Revenue = g.Sum(s => s.GrossSaleAmount),
                PercentageOfTotal = totalQuantity > 0 ? (g.Sum(s => s.Quantity) / totalQuantity) * 100 : 0
            })
            .OrderByDescending(p => p.Quantity)
            .ToList();

        // Production by Layer
        var productionByLayer = sales
            .GroupBy(s => s.Layer?.LayerLevel ?? "Unknown")
            .Select(g => new ProductionByCategory
            {
                Category = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count(),
                Revenue = g.Sum(s => s.GrossSaleAmount),
                PercentageOfTotal = totalQuantity > 0 ? (g.Sum(s => s.Quantity) / totalQuantity) * 100 : 0
            })
            .OrderByDescending(p => p.Quantity)
            .ToList();

        // Production by Destination (County)
        var productionByDestination = sales
            .Where(s => !string.IsNullOrEmpty(s.Destination))
            .GroupBy(s => s.Destination!)
            .Select(g => new ProductionByCategory
            {
                Category = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count(),
                Revenue = g.Sum(s => s.GrossSaleAmount),
                PercentageOfTotal = totalQuantity > 0 ? (g.Sum(s => s.Quantity) / totalQuantity) * 100 : 0
            })
            .OrderByDescending(p => p.Quantity)
            .ToList();

        // Production by Client
        var productionByClient = sales
            .Where(s => !string.IsNullOrEmpty(s.ClientName))
            .GroupBy(s => s.ClientName!.Trim())
            .Select(g => new ClientProductionData
            {
                ClientName = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count(),
                Revenue = g.Sum(s => s.GrossSaleAmount),
                PercentageOfTotal = totalQuantity > 0 ? (g.Sum(s => s.Quantity) / totalQuantity) * 100 : 0,
                FirstOrderDate = g.Min(s => s.SaleDate) ?? DateTime.MinValue,
                LastOrderDate = g.Max(s => s.SaleDate) ?? DateTime.MinValue
            })
            .OrderByDescending(p => p.Quantity)
            .ToList();

        // Daily Production Trend
        var dailyProduction = sales
            .GroupBy(s => s.SaleDate?.Date ?? DateTime.MinValue)
            .Where(g => g.Key != DateTime.MinValue)
            .Select(g => new DailyProductionData
            {
                Date = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count(),
                Revenue = g.Sum(s => s.GrossSaleAmount)
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Product-Layer Matrix (cross-reference)
        var productLayerMatrix = sales
            .GroupBy(s => new { Product = s.Product?.ProductName ?? "Unknown", Layer = s.Layer?.LayerLevel ?? "Unknown" })
            .Select(g => new ProductLayerData
            {
                ProductName = g.Key.Product,
                LayerLevel = g.Key.Layer,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count()
            })
            .OrderBy(p => p.ProductName)
            .ThenBy(p => p.LayerLevel)
            .ToList();

        // Top Vehicles by Volume
        var topVehicles = sales
            .GroupBy(s => s.VehicleRegistration.ToUpper().Trim())
            .Select(g => new VehicleProductionData
            {
                VehicleRegistration = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                OrderCount = g.Count(),
                Revenue = g.Sum(s => s.GrossSaleAmount),
                ClientName = g.FirstOrDefault(s => !string.IsNullOrEmpty(s.ClientName))?.ClientName
            })
            .OrderByDescending(v => v.Quantity)
            .Take(20)
            .ToList();

        // Calculate averages
        var daysInRange = (toDate - fromDate).Days + 1;
        var avgDailyQuantity = daysInRange > 0 ? totalQuantity / daysInRange : 0;
        var avgOrderSize = totalOrders > 0 ? totalQuantity / totalOrders : 0;

        return new ProductionDashboardData
        {
            // Summary Stats
            TotalQuantity = totalQuantity,
            TotalOrders = totalOrders,
            TotalRevenue = totalRevenue,
            UniqueClients = uniqueClients,
            UniqueVehicles = uniqueVehicles,
            AverageDailyQuantity = avgDailyQuantity,
            AverageOrderSize = avgOrderSize,

            // Breakdown Data
            ProductionByProduct = productionByProduct,
            ProductionByLayer = productionByLayer,
            ProductionByDestination = productionByDestination,
            ProductionByClient = productionByClient,
            DailyProduction = dailyProduction,
            ProductLayerMatrix = productLayerMatrix,
            TopVehicles = topVehicles,

            // Date Range
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    /// <summary>
    /// Get detailed sales data for export purposes.
    /// </summary>
    public async Task<List<ProductionExportRow>> GetProductionExportDataAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .OrderBy(s => s.SaleDate)
            .ThenBy(s => s.DateCreated)
            .ToListAsync();

        return sales.Select(s => new ProductionExportRow
        {
            Date = s.SaleDate ?? DateTime.MinValue,
            VehicleRegistration = s.VehicleRegistration,
            ClientName = s.ClientName ?? "",
            ClientPhone = s.ClientPhone ?? "",
            Destination = s.Destination ?? "",
            ProductName = s.Product?.ProductName ?? "Unknown",
            LayerLevel = s.Layer?.LayerLevel ?? "Unknown",
            Quantity = s.Quantity,
            PricePerUnit = s.PricePerUnit,
            TotalAmount = s.GrossSaleAmount,
            PaymentStatus = s.PaymentStatus ?? "Unknown",
            PaymentMode = s.PaymentMode ?? "Unknown"
        }).ToList();
    }
}

#region DTOs

/// <summary>
/// Comprehensive production dashboard data.
/// </summary>
public class ProductionDashboardData
{
    // Summary Statistics
    public double TotalQuantity { get; set; }
    public int TotalOrders { get; set; }
    public double TotalRevenue { get; set; }
    public int UniqueClients { get; set; }
    public int UniqueVehicles { get; set; }
    public double AverageDailyQuantity { get; set; }
    public double AverageOrderSize { get; set; }

    // Breakdown Data
    public List<ProductionByCategory> ProductionByProduct { get; set; } = [];
    public List<ProductionByCategory> ProductionByLayer { get; set; } = [];
    public List<ProductionByCategory> ProductionByDestination { get; set; } = [];
    public List<ClientProductionData> ProductionByClient { get; set; } = [];
    public List<DailyProductionData> DailyProduction { get; set; } = [];
    public List<ProductLayerData> ProductLayerMatrix { get; set; } = [];
    public List<VehicleProductionData> TopVehicles { get; set; } = [];

    // Date Range
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

/// <summary>
/// Generic production breakdown by category.
/// </summary>
public class ProductionByCategory
{
    public string Category { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public int OrderCount { get; set; }
    public double Revenue { get; set; }
    public double PercentageOfTotal { get; set; }
}

/// <summary>
/// Client-specific production data.
/// </summary>
public class ClientProductionData
{
    public string ClientName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public int OrderCount { get; set; }
    public double Revenue { get; set; }
    public double PercentageOfTotal { get; set; }
    public DateTime FirstOrderDate { get; set; }
    public DateTime LastOrderDate { get; set; }
}

/// <summary>
/// Daily production trend data.
/// </summary>
public class DailyProductionData
{
    public DateTime Date { get; set; }
    public double Quantity { get; set; }
    public int OrderCount { get; set; }
    public double Revenue { get; set; }
}

/// <summary>
/// Product-Layer cross-reference data.
/// </summary>
public class ProductLayerData
{
    public string ProductName { get; set; } = string.Empty;
    public string LayerLevel { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public int OrderCount { get; set; }
}

/// <summary>
/// Vehicle production data.
/// </summary>
public class VehicleProductionData
{
    public string VehicleRegistration { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public double Quantity { get; set; }
    public int OrderCount { get; set; }
    public double Revenue { get; set; }
}

/// <summary>
/// Row data for production export.
/// </summary>
public class ProductionExportRow
{
    public DateTime Date { get; set; }
    public string VehicleRegistration { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string LayerLevel { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double PricePerUnit { get; set; }
    public double TotalAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string PaymentMode { get; set; } = string.Empty;
}

#endregion
