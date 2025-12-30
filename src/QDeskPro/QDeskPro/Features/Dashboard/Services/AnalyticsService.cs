namespace QDeskPro.Features.Dashboard.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for manager/admin analytics dashboard
/// Implements the same 4-source expense model as the clerk report for consistency
/// </summary>
public class AnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AnalyticsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Get analytics dashboard statistics for a date range and quarry
    /// Uses 4-source expense model: Manual + Commission + Loaders Fee + Land Rate
    /// </summary>
    public async Task<AnalyticsDashboardStats> GetDashboardStatsAsync(string? quarryId, DateTime fromDate, DateTime toDate)
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
            .ToListAsync();

        // Base query for expenses (manual user entries)
        var expensesQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expensesQuery = expensesQuery.Where(e => e.QId == quarryId);
        }

        var expenses = await expensesQuery.ToListAsync();

        // Fuel usage query
        var fuelQuery = context.FuelUsages
            .Where(f => f.IsActive)
            .Where(f => string.Compare(f.DateStamp, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            fuelQuery = fuelQuery.Where(f => f.QId == quarryId);
        }

        var fuelUsages = await fuelQuery.ToListAsync();

        // Banking query
        var bankingQuery = context.Bankings
            .Where(b => b.IsActive)
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0)
            .Where(b => string.Compare(b.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            bankingQuery = bankingQuery.Where(b => b.QId == quarryId);
        }

        var bankings = await bankingQuery.ToListAsync();

        // Calculate basic stats
        var totalRevenue = sales.Sum(s => s.GrossSaleAmount);
        var totalOrders = sales.Count;
        var totalQuantity = sales.Sum(s => s.Quantity);
        var totalBanked = bankings.Sum(b => b.AmountBanked);

        // Calculate total expenses using 4-source model (same as clerk report)
        var manualExpenses = expenses.Sum(e => e.Amount);
        var commission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);

        // Get quarry for fee calculations
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        double loadersFee = 0;
        double landRateFee = 0;

        if (quarry != null)
        {
            if (quarry.LoadersFee.HasValue)
            {
                // Don't apply loaders fee for beam or hardcore products
                loadersFee = sales
                    .Where(s =>
                    {
                        var productName = s.Product?.ProductName ?? "";
                        return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                               !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                    })
                    .Sum(s => s.Quantity * quarry.LoadersFee.Value);
            }

            if (quarry.LandRateFee.HasValue && quarry.LandRateFee.Value > 0)
            {
                foreach (var sale in sales)
                {
                    // Skip land rate if the sale has it excluded
                    if (!sale.IncludeLandRate)
                        continue;

                    var product = sale.Product;
                    if (product?.ProductName?.ToLower().Contains("reject") == true && quarry.RejectsFee.HasValue)
                    {
                        landRateFee += sale.Quantity * quarry.RejectsFee.Value;
                    }
                    else
                    {
                        landRateFee += sale.Quantity * quarry.LandRateFee.Value;
                    }
                }
            }
        }

        var totalExpenses = manualExpenses + commission + loadersFee + landRateFee;

        // Calculate unpaid orders
        var unpaidOrders = sales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);

        // Get Cash In Hand (B/F) - the closing balance from the day BEFORE the last date in the range
        // For a Dec 1-10 report, this would be Dec 9's closing balance (since balances carry over daily)
        double openingBalance = 0;
        if (!string.IsNullOrEmpty(quarryId))
        {
            // Cash In Hand (B/F) = closing balance from day before toDate (e.g., Dec 9 for Dec 1-10 report)
            var previousDayStamp = toDate.AddDays(-1).ToString("yyyyMMdd");

            var previousDayNote = await context.DailyNotes
                .Where(n => n.DateStamp == previousDayStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            openingBalance = previousDayNote?.ClosingBalance ?? 0;
        }

        // Get Collections (payments received during period for sales made before period)
        var collectionsQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "Paid")
            .Where(s => s.PaymentReceivedDate >= fromDate && s.PaymentReceivedDate <= toDate)
            .Where(s => s.SaleDate < fromDate); // Sale was made before period

        if (!string.IsNullOrEmpty(quarryId))
        {
            collectionsQuery = collectionsQuery.Where(s => s.QId == quarryId);
        }

        // Use actual database columns instead of computed property for EF Core translation
        var totalCollections = await collectionsQuery.SumAsync(s => s.Quantity * s.PricePerUnit);

        // Get Prepayments (customer deposits received during period)
        var prepaymentsQuery = context.Prepayments
            .Where(p => p.IsActive)
            .Where(p => p.PrepaymentDate >= fromDate && p.PrepaymentDate <= toDate);

        if (!string.IsNullOrEmpty(quarryId))
        {
            prepaymentsQuery = prepaymentsQuery.Where(p => p.QId == quarryId);
        }

        var totalPrepayments = await prepaymentsQuery.SumAsync(p => p.TotalAmountPaid);

        // Net Income formula: (Earnings + Opening Balance + Collections + Prepayments) - Unpaid Orders
        // Where Earnings = TotalSales - TotalExpenses
        var netIncome = (totalRevenue - totalExpenses + openingBalance + totalCollections + totalPrepayments) - unpaidOrders;
        var profitMargin = totalRevenue > 0 ? (netIncome / totalRevenue) * 100 : 0;

        var totalFuelConsumed = fuelUsages.Sum(f => f.MachinesLoaded + f.WheelLoadersLoaded);

        // Calculate daily averages and additional metrics
        var days = (toDate.Date - fromDate.Date).Days + 1;
        var dailyAverageRevenue = days > 0 ? totalRevenue / days : 0;
        var dailyAverageOrders = days > 0 ? (double)totalOrders / days : 0;
        var dailyAveragePieces = days > 0 ? totalQuantity / days : 0;
        var dailyAverageBanked = days > 0 ? totalBanked / days : 0;
        var dailyAverageFuel = days > 0 ? totalFuelConsumed / days : 0;

        // Calculate per-piece metrics
        var avgCostPerPiece = totalQuantity > 0 ? totalExpenses / totalQuantity : 0;
        var avgRevenuePerPiece = totalQuantity > 0 ? totalRevenue / totalQuantity : 0;
        var litersPerPiece = totalQuantity > 0 ? totalFuelConsumed / totalQuantity : 0;

        // Calculate enhanced KPI metrics
        var fuelEfficiency = totalFuelConsumed > 0 ? totalQuantity / totalFuelConsumed : 0;  // Pieces per Liter
        var profitPerPiece = totalQuantity > 0 ? netIncome / totalQuantity : 0;
        var collectionRate = totalRevenue > 0 ? ((totalRevenue - unpaidOrders) / totalRevenue) * 100 : 0;
        var avgOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0;
        var commissionRate = totalRevenue > 0 ? (commission / totalRevenue) * 100 : 0;
        var grossMargin = totalRevenue > 0 ? ((totalRevenue - totalExpenses) / totalRevenue) * 100 : 0;
        var avgQuantityPerOrder = totalOrders > 0 ? totalQuantity / totalOrders : 0;

        var stats = new AnalyticsDashboardStats
        {
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,
            TotalQuantity = totalQuantity,
            TotalExpenses = totalExpenses,
            ManualExpenses = manualExpenses,
            Commission = commission,
            LoadersFee = loadersFee,
            LandRateFee = landRateFee,
            OpeningBalance = openingBalance,
            UnpaidOrders = unpaidOrders,
            NetIncome = netIncome,
            ProfitMargin = profitMargin,
            TotalFuelConsumed = totalFuelConsumed,
            TotalBanked = totalBanked,
            DailyAverageRevenue = dailyAverageRevenue,
            DailyAverageOrders = dailyAverageOrders,
            DailyAveragePieces = dailyAveragePieces,
            DailyAverageBanked = dailyAverageBanked,
            DailyAverageFuel = dailyAverageFuel,
            AvgCostPerPiece = avgCostPerPiece,
            AvgRevenuePerPiece = avgRevenuePerPiece,
            LitersPerPiece = litersPerPiece,
            // Enhanced KPIs
            FuelEfficiency = fuelEfficiency,
            ProfitPerPiece = profitPerPiece,
            CollectionRate = collectionRate,
            AvgOrderValue = avgOrderValue,
            CommissionRate = commissionRate,
            GrossMargin = grossMargin,
            AvgQuantityPerOrder = avgQuantityPerOrder,
            DateRangeDays = days
        };

        return stats;
    }

    /// <summary>
    /// Get sales trends data for chart visualization
    /// Uses 4-source expense model for accurate expense tracking per day
    /// </summary>
    public async Task<SalesTrendsData> GetSalesTrendsAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Base query
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
            .ToListAsync();

        var expensesQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expensesQuery = expensesQuery.Where(e => e.QId == quarryId);
        }

        var expenses = await expensesQuery.ToListAsync();

        // Get quarry for fee calculations
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        // Group sales by date
        var salesByDate = sales
            .GroupBy(s => s.SaleDate?.Date ?? DateTime.Today)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group manual expenses by date
        var manualExpensesByDate = expenses
            .GroupBy(e => e.ExpenseDate?.Date ?? DateTime.Today)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        // Fill in all dates with complete expense calculation (4-source model)
        var allDates = new List<DailySalesData>();
        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            var daySales = salesByDate.ContainsKey(date) ? salesByDate[date] : new List<Sale>();
            var manualExpense = manualExpensesByDate.ContainsKey(date) ? manualExpensesByDate[date] : 0;

            // Calculate auto-generated expenses for this day's sales
            var commission = daySales.Sum(s => s.Quantity * s.CommissionPerUnit);
            double loadersFee = 0;
            double landRateFee = 0;

            if (quarry != null)
            {
                if (quarry.LoadersFee.HasValue)
                {
                    // Don't apply loaders fee for beam or hardcore products
                    loadersFee = daySales
                        .Where(s =>
                        {
                            var productName = s.Product?.ProductName ?? "";
                            return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                                   !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                        })
                        .Sum(s => s.Quantity * quarry.LoadersFee.Value);
                }

                if (quarry.LandRateFee.HasValue && quarry.LandRateFee.Value > 0)
                {
                    foreach (var sale in daySales)
                    {
                        // Skip land rate if the sale has it excluded
                        if (!sale.IncludeLandRate)
                            continue;

                        var product = sale.Product;
                        if (product?.ProductName?.ToLower().Contains("reject") == true && quarry.RejectsFee.HasValue)
                        {
                            landRateFee += sale.Quantity * quarry.RejectsFee.Value;
                        }
                        else
                        {
                            landRateFee += sale.Quantity * quarry.LandRateFee.Value;
                        }
                    }
                }
            }

            var totalDayExpenses = manualExpense + commission + loadersFee + landRateFee;

            allDates.Add(new DailySalesData
            {
                Date = date,
                Revenue = daySales.Sum(s => s.GrossSaleAmount),
                Expenses = totalDayExpenses,
                Orders = daySales.Count,
                Quantity = daySales.Sum(s => s.Quantity)
            });
        }

        var trendsData = new SalesTrendsData
        {
            DailyData = allDates,
            Labels = allDates.Select(d => d.Date.ToString("dd/MM")).ToList(),
            RevenueData = allDates.Select(d => d.Revenue).ToList(),
            ExpensesData = allDates.Select(d => d.Expenses).ToList()
        };

        return trendsData;
    }

    /// <summary>
    /// Get product breakdown data for pie chart
    /// </summary>
    public async Task<ProductBreakdownData> GetProductBreakdownAsync(string? quarryId, DateTime fromDate, DateTime toDate)
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
            .ToListAsync();

        var productData = sales
            .GroupBy(s => s.Product?.ProductName ?? "Unknown")
            .Select(g => new ProductSalesData
            {
                ProductName = g.Key,
                Quantity = g.Sum(s => s.Quantity),
                Revenue = g.Sum(s => s.GrossSaleAmount),
                Orders = g.Count()
            })
            .OrderByDescending(p => p.Revenue)
            .ToList();

        var breakdownData = new ProductBreakdownData
        {
            Products = productData,
            Labels = productData.Select(p => p.ProductName).ToList(),
            RevenueData = productData.Select(p => p.Revenue).ToList(),
            QuantityData = productData.Select(p => p.Quantity).ToList()
        };

        return breakdownData;
    }

    /// <summary>
    /// Get detailed daily breakdown for table
    /// Uses 4-source expense model: Manual + Commission + Loaders Fee + Land Rate
    /// </summary>
    public async Task<List<DailySummary>> GetDailyBreakdownAsync(string? quarryId, DateTime fromDate, DateTime toDate)
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
            .ToListAsync();

        var expensesQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expensesQuery = expensesQuery.Where(e => e.QId == quarryId);
        }

        var expenses = await expensesQuery.ToListAsync();

        // Get quarry for fee calculations
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        // Group by date
        var salesByDate = sales
            .GroupBy(s => s.SaleDate?.Date ?? DateTime.Today)
            .ToDictionary(g => g.Key, g => g.ToList());

        var manualExpensesByDate = expenses
            .GroupBy(e => e.ExpenseDate?.Date ?? DateTime.Today)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        // Create daily summaries with 4-source expense model
        var summaries = new List<DailySummary>();
        for (var date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
        {
            var daySales = salesByDate.ContainsKey(date) ? salesByDate[date] : new List<Sale>();
            var manualExpense = manualExpensesByDate.ContainsKey(date) ? manualExpensesByDate[date] : 0;

            // Calculate auto-generated expenses for this day's sales
            var commission = daySales.Sum(s => s.Quantity * s.CommissionPerUnit);
            double loadersFee = 0;
            double landRateFee = 0;

            if (quarry != null)
            {
                if (quarry.LoadersFee.HasValue)
                {
                    // Don't apply loaders fee for beam or hardcore products
                    loadersFee = daySales
                        .Where(s =>
                        {
                            var productName = s.Product?.ProductName ?? "";
                            return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                                   !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                        })
                        .Sum(s => s.Quantity * quarry.LoadersFee.Value);
                }

                if (quarry.LandRateFee.HasValue && quarry.LandRateFee.Value > 0)
                {
                    foreach (var sale in daySales)
                    {
                        // Skip land rate if the sale has it excluded
                        if (!sale.IncludeLandRate)
                            continue;

                        var product = sale.Product;
                        if (product?.ProductName?.ToLower().Contains("reject") == true && quarry.RejectsFee.HasValue)
                        {
                            landRateFee += sale.Quantity * quarry.RejectsFee.Value;
                        }
                        else
                        {
                            landRateFee += sale.Quantity * quarry.LandRateFee.Value;
                        }
                    }
                }
            }

            var totalDayExpenses = manualExpense + commission + loadersFee + landRateFee;
            var revenue = daySales.Sum(s => s.GrossSaleAmount);
            var quantity = daySales.Sum(s => s.Quantity);
            var orders = daySales.Count;
            var netAmount = revenue - totalDayExpenses;

            summaries.Add(new DailySummary
            {
                Date = date,
                Orders = orders,
                Quantity = quantity,
                Revenue = revenue,
                Expenses = totalDayExpenses,
                NetAmount = netAmount
            });
        }

        return summaries.OrderBy(s => s.Date).ToList();
    }
}

/// <summary>
/// Analytics dashboard statistics model
/// Includes all metrics needed for main cards and small metric cards
/// </summary>
public class AnalyticsDashboardStats
{
    // Core metrics
    public double TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public double TotalQuantity { get; set; }
    public double TotalFuelConsumed { get; set; }
    public double TotalBanked { get; set; }

    // Expense breakdown (4-source model)
    public double TotalExpenses { get; set; }
    public double ManualExpenses { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }

    // Cash In Hand (B/F) and unpaid orders (for consistent Net Income calculation)
    public double OpeningBalance { get; set; }
    public double UnpaidOrders { get; set; }

    // Calculated metrics
    // Net Income = (Earnings + Cash In Hand) - Unpaid Orders
    public double NetIncome { get; set; }
    public double ProfitMargin { get; set; }

    // Daily averages
    public double DailyAverageRevenue { get; set; }
    public double DailyAverageOrders { get; set; }
    public double DailyAveragePieces { get; set; }
    public double DailyAverageBanked { get; set; }
    public double DailyAverageFuel { get; set; }

    // Per-piece metrics
    public double AvgCostPerPiece { get; set; }
    public double AvgRevenuePerPiece { get; set; }
    public double LitersPerPiece { get; set; }

    // Enhanced KPI metrics
    public double FuelEfficiency { get; set; }      // Pieces per Liter (Total Pieces / Total Fuel)
    public double ProfitPerPiece { get; set; }      // Net Income / Total Quantity
    public double CollectionRate { get; set; }      // (Revenue - Unpaid) / Revenue * 100
    public double AvgOrderValue { get; set; }       // Total Revenue / Total Orders
    public double CommissionRate { get; set; }      // Commission / Revenue * 100
    public double GrossMargin { get; set; }         // (Revenue - Total Expenses) / Revenue * 100
    public double AvgQuantityPerOrder { get; set; } // Total Quantity / Total Orders

    // Date range info
    public int DateRangeDays { get; set; }
}

/// <summary>
/// Sales trends data for charting
/// </summary>
public class SalesTrendsData
{
    public List<DailySalesData> DailyData { get; set; } = new();
    public List<string> Labels { get; set; } = new();
    public List<double> RevenueData { get; set; } = new();
    public List<double> ExpensesData { get; set; } = new();
}

/// <summary>
/// Daily sales data point
/// </summary>
public class DailySalesData
{
    public DateTime Date { get; set; }
    public double Revenue { get; set; }
    public double Expenses { get; set; }
    public int Orders { get; set; }
    public double Quantity { get; set; }
}

/// <summary>
/// Product breakdown data for pie chart
/// </summary>
public class ProductBreakdownData
{
    public List<ProductSalesData> Products { get; set; } = new();
    public List<string> Labels { get; set; } = new();
    public List<double> RevenueData { get; set; } = new();
    public List<double> QuantityData { get; set; } = new();
}

/// <summary>
/// Product sales data
/// </summary>
public class ProductSalesData
{
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Revenue { get; set; }
    public int Orders { get; set; }
}

/// <summary>
/// Daily summary for table display
/// </summary>
public class DailySummary
{
    public DateTime Date { get; set; }
    public int Orders { get; set; }
    public double Quantity { get; set; }
    public double Revenue { get; set; }
    public double Expenses { get; set; }
    public double NetAmount { get; set; }
}
