using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

namespace QDeskPro.Features.Dashboard.Services;

public class DataAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DataAnalyticsService> _logger;

    public DataAnalyticsService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<DataAnalyticsService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Main method - returns complete analytics result with all metrics
    /// </summary>
    public async Task<DataAnalyticsResult> GetDataAnalyticsAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate,
        bool includeComparison = false)
    {
        var cacheKey = $"data-analytics:{quarryId}:{fromDate:yyyyMMdd}:{toDate:yyyyMMdd}:{includeComparison}";

        if (_cache.TryGetValue(cacheKey, out DataAnalyticsResult? cachedResult) && cachedResult != null)
        {
            return cachedResult;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Build base query with multi-tenant isolation
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
            .Include(s => s.Broker)
            .ToListAsync();

        // Get quarry for fee configurations
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        // Calculate basic metrics
        var totalRevenue = sales.Sum(s => s.Quantity * s.PricePerUnit);
        var totalQuantity = sales.Sum(s => s.Quantity);

        // Get expenses (4-source model)
        var manualExpenses = await context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => string.IsNullOrEmpty(quarryId) || e.QId == quarryId)
            .SumAsync(e => e.Amount);

        var commission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);
        var loadersFee = quarry?.LoadersFee != null
            ? sales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
            : 0;

        // Land rate fee with Reject product handling
        // CRITICAL: Only charge land rate if sale.IncludeLandRate is true
        var landRateFee = 0.0;
        if (quarry?.LandRateFee != null && quarry.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate if excluded for this sale
                if (!sale.IncludeLandRate)
                    continue;

                var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
                var rate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                landRateFee += sale.Quantity * rate;
            }
        }

        var totalExpenses = manualExpenses + commission + loadersFee + landRateFee;
        var netIncome = totalRevenue - totalExpenses;

        // Get fuel cost analysis
        var fuelAnalysis = await GetFuelCostAnalysisAsync(quarryId, fromDate, toDate);

        // Add fuel cost to total expenses if available
        if (fuelAnalysis.TotalFuelCost > 0)
        {
            totalExpenses += fuelAnalysis.TotalFuelCost;
            netIncome = totalRevenue - totalExpenses;
        }

        // Get cost breakdown per piece
        var costBreakdown = await GetCostBreakdownPerPieceAsync(quarryId, fromDate, toDate);

        // Get product profitability
        var productProfitability = await GetProductProfitabilityAsync(quarryId, fromDate, toDate);

        // Get efficiency metrics
        var efficiency = await GetEfficiencyMetricsAsync(quarryId, fromDate, toDate);

        // Get comparative period analysis if requested
        ComparativePeriodAnalysis? comparison = null;
        if (includeComparison)
        {
            comparison = await GetComparativePeriodAnalysisAsync(quarryId, fromDate, toDate);
        }

        var dayCount = (toDate - fromDate).Days + 1;

        var result = new DataAnalyticsResult
        {
            TotalRevenue = totalRevenue,
            TotalQuantity = totalQuantity,
            TotalExpenses = totalExpenses,
            NetIncome = netIncome,
            FuelAnalysis = fuelAnalysis,
            CostBreakdown = costBreakdown,
            ProductProfitability = productProfitability,
            Efficiency = efficiency,
            Comparison = comparison,
            FromDate = fromDate,
            ToDate = toDate,
            DayCount = dayCount,
            HasFuelCostData = fuelAnalysis.TotalFuelCost > 0
        };

        // Cache for 5 minutes
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

        return result;
    }

    /// <summary>
    /// Fuel cost analysis with daily trends
    /// </summary>
    public async Task<FuelCostAnalysis> GetFuelCostAnalysisAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Get fuel usage data
        var fuelUsageQuery = context.FuelUsages
            .Where(f => f.IsActive)
            .Where(f => string.Compare(f.DateStamp, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            fuelUsageQuery = fuelUsageQuery.Where(f => f.QId == quarryId);
        }

        var fuelUsages = await fuelUsageQuery.ToListAsync();

        var totalFuelConsumed = fuelUsages.Sum(f => f.MachinesLoaded + f.WheelLoadersLoaded);

        // Get quarry to check if fuel cost is configured
        Quarry? quarry = null;
        double fuelCostPerLiter = 0;
        double totalFuelCost = 0;

        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
            if (quarry?.FuelCostPerLiter != null && quarry.FuelCostPerLiter > 0)
            {
                fuelCostPerLiter = quarry.FuelCostPerLiter.Value;
                totalFuelCost = totalFuelConsumed * fuelCostPerLiter;
            }
        }

        // Get total quantity sold for fuel efficiency calculation
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var totalQuantity = await salesQuery.SumAsync(s => s.Quantity);

        var fuelCostPerPiece = totalQuantity > 0 && totalFuelCost > 0
            ? totalFuelCost / totalQuantity
            : 0;

        var fuelEfficiency = totalFuelConsumed > 0
            ? totalQuantity / totalFuelConsumed
            : 0;

        // Build daily trend data
        var dailyTrend = new List<DailyFuelData>();

        foreach (var usage in fuelUsages.OrderBy(f => f.UsageDate))
        {
            if (usage.UsageDate.HasValue)
            {
                var dailyConsumed = usage.MachinesLoaded + usage.WheelLoadersLoaded;
                var dailyCost = fuelCostPerLiter > 0 ? dailyConsumed * fuelCostPerLiter : 0;

                // Get quantity sold on this date
                var dateStamp = usage.UsageDate.Value.ToString("yyyyMMdd");
                var dailyQuantity = await context.Sales
                    .Where(s => s.IsActive && s.DateStamp == dateStamp)
                    .Where(s => string.IsNullOrEmpty(quarryId) || s.QId == quarryId)
                    .SumAsync(s => s.Quantity);

                var dailyEfficiency = dailyConsumed > 0 ? dailyQuantity / dailyConsumed : 0;

                dailyTrend.Add(new DailyFuelData
                {
                    Date = usage.UsageDate.Value,
                    FuelConsumed = dailyConsumed,
                    FuelCost = dailyCost,
                    Quantity = dailyQuantity,
                    Efficiency = dailyEfficiency
                });
            }
        }

        return new FuelCostAnalysis
        {
            TotalFuelConsumed = totalFuelConsumed,
            FuelCostPerLiter = fuelCostPerLiter,
            TotalFuelCost = totalFuelCost,
            FuelCostPerPiece = fuelCostPerPiece,
            FuelEfficiency = fuelEfficiency,
            DailyTrend = dailyTrend
        };
    }

    /// <summary>
    /// Cost breakdown per piece (commission, loaders, land rate, fuel, manual)
    /// </summary>
    public async Task<CostBreakdownPerPiece> GetCostBreakdownPerPieceAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Get sales
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery.Include(s => s.Product).ToListAsync();
        var totalQuantity = sales.Sum(s => s.Quantity);

        if (totalQuantity == 0)
        {
            return new CostBreakdownPerPiece();
        }

        // Get quarry for fees
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        // Calculate total costs by category
        var totalCommission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);

        var totalLoadersFee = quarry?.LoadersFee != null
            ? sales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
            : 0;

        var totalLandRateFee = 0.0;
        if (quarry?.LandRateFee != null && quarry.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate if excluded for this sale
                if (!sale.IncludeLandRate)
                    continue;

                var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
                var rate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                totalLandRateFee += sale.Quantity * rate;
            }
        }

        // Get manual expenses
        var totalManualExpenses = await context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => string.IsNullOrEmpty(quarryId) || e.QId == quarryId)
            .SumAsync(e => e.Amount);

        // Get fuel cost
        var fuelAnalysis = await GetFuelCostAnalysisAsync(quarryId, fromDate, toDate);
        var totalFuelCost = fuelAnalysis.TotalFuelCost;

        // Calculate revenue
        var totalRevenue = sales.Sum(s => s.Quantity * s.PricePerUnit);

        // Per-piece calculations
        var commissionPerPiece = totalCommission / totalQuantity;
        var loadersFeePerPiece = totalLoadersFee / totalQuantity;
        var landRatePerPiece = totalLandRateFee / totalQuantity;
        var manualExpensePerPiece = totalManualExpenses / totalQuantity;
        var fuelCostPerPiece = totalFuelCost / totalQuantity;
        var revenuePerPiece = totalRevenue / totalQuantity;

        var totalCostPerPiece = commissionPerPiece + loadersFeePerPiece + landRatePerPiece
            + manualExpensePerPiece + fuelCostPerPiece;

        var netMarginPerPiece = revenuePerPiece - totalCostPerPiece;
        var marginPercent = revenuePerPiece > 0 ? (netMarginPerPiece / revenuePerPiece) * 100 : 0;

        return new CostBreakdownPerPiece
        {
            TotalCostPerPiece = totalCostPerPiece,
            CommissionPerPiece = commissionPerPiece,
            LoadersFeePerPiece = loadersFeePerPiece,
            LandRatePerPiece = landRatePerPiece,
            ManualExpensePerPiece = manualExpensePerPiece,
            FuelCostPerPiece = fuelCostPerPiece,
            RevenuePerPiece = revenuePerPiece,
            NetMarginPerPiece = netMarginPerPiece,
            MarginPercent = marginPercent
        };
    }

    /// <summary>
    /// Operating cost trends over time with configurable granularity
    /// </summary>
    public async Task<OperatingCostTrends> GetOperatingCostTrendsAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate,
        TrendGranularity granularity = TrendGranularity.Daily)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Get sales
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery.Include(s => s.Product).ToListAsync();

        // Get expenses
        var expensesQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expensesQuery = expensesQuery.Where(e => e.QId == quarryId);
        }

        var expenses = await expensesQuery.ToListAsync();

        // Get fuel usage
        var fuelQuery = context.FuelUsages
            .Where(f => f.IsActive)
            .Where(f => string.Compare(f.DateStamp, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            fuelQuery = fuelQuery.Where(f => f.QId == quarryId);
        }

        var fuelUsages = await fuelQuery.ToListAsync();

        // Get quarry for fees
        Quarry? quarry = null;
        double fuelCostPerLiter = 0;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
            fuelCostPerLiter = quarry?.FuelCostPerLiter ?? 0;
        }

        // Group data by period
        var trendData = new List<CostTrendPoint>();
        var currentDate = fromDate;

        while (currentDate <= toDate)
        {
            var periodEnd = granularity switch
            {
                TrendGranularity.Daily => currentDate,
                TrendGranularity.Weekly => currentDate.AddDays(6),
                TrendGranularity.Monthly => new DateTime(currentDate.Year, currentDate.Month, DateTime.DaysInMonth(currentDate.Year, currentDate.Month)),
                _ => currentDate
            };

            if (periodEnd > toDate) periodEnd = toDate;

            var periodStartStamp = currentDate.ToString("yyyyMMdd");
            var periodEndStamp = periodEnd.ToString("yyyyMMdd");

            // Get period sales
            var periodSales = sales
                .Where(s => s.SaleDate.HasValue && s.SaleDate.Value >= currentDate && s.SaleDate.Value <= periodEnd)
                .ToList();

            var periodQuantity = periodSales.Sum(s => s.Quantity);

            // Calculate period costs
            var periodManualExpenses = expenses
                .Where(e => e.ExpenseDate.HasValue && e.ExpenseDate.Value >= currentDate && e.ExpenseDate.Value <= periodEnd)
                .Sum(e => e.Amount);

            var periodCommission = periodSales.Sum(s => s.Quantity * s.CommissionPerUnit);

            var periodLoadersFee = quarry?.LoadersFee != null
                ? periodSales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
                : 0;

            var periodLandRateFee = 0.0;
            if (quarry?.LandRateFee != null && quarry.LandRateFee > 0)
            {
                foreach (var sale in periodSales)
                {
                    // Skip land rate if excluded for this sale
                    if (!sale.IncludeLandRate)
                        continue;

                    var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
                    var rate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                    periodLandRateFee += sale.Quantity * rate;
                }
            }

            var periodFuelConsumed = fuelUsages
                .Where(f => f.UsageDate.HasValue && f.UsageDate.Value >= currentDate && f.UsageDate.Value <= periodEnd)
                .Sum(f => f.MachinesLoaded + f.WheelLoadersLoaded);

            var periodFuelCost = fuelCostPerLiter > 0 ? periodFuelConsumed * fuelCostPerLiter : 0;

            var periodTotalCost = periodManualExpenses + periodCommission + periodLoadersFee
                + periodLandRateFee + periodFuelCost;

            var costPerPiece = periodQuantity > 0 ? periodTotalCost / periodQuantity : 0;

            var periodLabel = granularity switch
            {
                TrendGranularity.Daily => currentDate.ToString("MMM dd"),
                TrendGranularity.Weekly => $"{currentDate:MMM dd} - {periodEnd:MMM dd}",
                TrendGranularity.Monthly => currentDate.ToString("MMM yyyy"),
                _ => currentDate.ToString("MMM dd")
            };

            trendData.Add(new CostTrendPoint
            {
                Period = currentDate,
                PeriodLabel = periodLabel,
                TotalCost = periodTotalCost,
                ManualExpenses = periodManualExpenses,
                Commission = periodCommission,
                LoadersFee = periodLoadersFee,
                LandRateFee = periodLandRateFee,
                FuelCost = periodFuelCost,
                Quantity = periodQuantity,
                CostPerPiece = costPerPiece
            });

            // Move to next period
            currentDate = granularity switch
            {
                TrendGranularity.Daily => currentDate.AddDays(1),
                TrendGranularity.Weekly => currentDate.AddDays(7),
                TrendGranularity.Monthly => currentDate.AddMonths(1),
                _ => currentDate.AddDays(1)
            };
        }

        return new OperatingCostTrends
        {
            Granularity = granularity,
            TrendData = trendData
        };
    }

    /// <summary>
    /// Product profitability analysis with detailed metrics per product
    /// </summary>
    public async Task<ProductProfitabilityAnalysis> GetProductProfitabilityAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Get sales with products
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery.Include(s => s.Product).ToListAsync();

        // Get quarry for fees
        Quarry? quarry = null;
        double fuelCostPerLiter = 0;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
            fuelCostPerLiter = quarry?.FuelCostPerLiter ?? 0;
        }

        // Get total fuel for fuel cost allocation
        var totalFuelConsumed = await context.FuelUsages
            .Where(f => f.IsActive)
            .Where(f => string.Compare(f.DateStamp, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp, toStamp) <= 0)
            .Where(f => string.IsNullOrEmpty(quarryId) || f.QId == quarryId)
            .SumAsync(f => f.MachinesLoaded + f.WheelLoadersLoaded);

        var totalFuelCost = fuelCostPerLiter > 0 ? totalFuelConsumed * fuelCostPerLiter : 0;
        var totalQuantityAllProducts = sales.Sum(s => s.Quantity);

        // Get manual expenses to allocate proportionally
        var totalManualExpenses = await context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => string.IsNullOrEmpty(quarryId) || e.QId == quarryId)
            .SumAsync(e => e.Amount);

        // Group by product
        var productGroups = sales.GroupBy(s => s.ProductId);
        var productProfitability = new List<ProductProfitability>();

        foreach (var group in productGroups)
        {
            var productSales = group.ToList();
            var product = productSales.First().Product;

            if (product == null) continue;

            var quantity = productSales.Sum(s => s.Quantity);
            var revenue = productSales.Sum(s => s.Quantity * s.PricePerUnit);
            var commission = productSales.Sum(s => s.Quantity * s.CommissionPerUnit);

            var loadersFee = quarry?.LoadersFee != null
                ? productSales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
                : 0;

            var isRejectProduct = product.ProductName?.ToLower().Contains("reject") == true;
            var landRatePerUnit = isRejectProduct
                ? (quarry?.RejectsFee ?? 0)
                : (quarry?.LandRateFee ?? 0);
            var landRateFee = productSales.Sum(s => s.Quantity * landRatePerUnit);

            // Allocate fuel cost proportionally based on quantity
            var fuelCostForProduct = totalQuantityAllProducts > 0
                ? (quantity / totalQuantityAllProducts) * totalFuelCost
                : 0;

            // Allocate manual expenses proportionally
            var manualExpensesForProduct = totalQuantityAllProducts > 0
                ? (quantity / totalQuantityAllProducts) * totalManualExpenses
                : 0;

            var totalCost = commission + loadersFee + landRateFee + fuelCostForProduct + manualExpensesForProduct;
            var netProfit = revenue - totalCost;
            var marginPercent = revenue > 0 ? (netProfit / revenue) * 100 : 0;

            var avgPricePerPiece = quantity > 0 ? revenue / quantity : 0;
            var avgCostPerPiece = quantity > 0 ? totalCost / quantity : 0;

            // Fuel efficiency for this product
            var fuelForProduct = totalQuantityAllProducts > 0
                ? (quantity / totalQuantityAllProducts) * totalFuelConsumed
                : 0;
            var fuelEfficiency = fuelForProduct > 0 ? quantity / fuelForProduct : 0;

            productProfitability.Add(new ProductProfitability
            {
                ProductName = product.ProductName ?? "Unknown",
                Quantity = quantity,
                Revenue = revenue,
                TotalCost = totalCost,
                NetProfit = netProfit,
                MarginPercent = marginPercent,
                AvgPricePerPiece = avgPricePerPiece,
                AvgCostPerPiece = avgCostPerPiece,
                FuelEfficiency = fuelEfficiency
            });
        }

        return new ProductProfitabilityAnalysis
        {
            Products = productProfitability.OrderByDescending(p => p.Revenue).ToList()
        };
    }

    /// <summary>
    /// Comparative period analysis (current vs previous period)
    /// </summary>
    public async Task<ComparativePeriodAnalysis> GetComparativePeriodAnalysisAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        // Calculate previous period (same duration)
        var duration = (toDate - fromDate).Days;
        var previousFromDate = fromDate.AddDays(-(duration + 1));
        var previousToDate = fromDate.AddDays(-1);

        // Get current period metrics
        var currentMetrics = await GetPeriodMetricsAsync(quarryId, fromDate, toDate);

        // Get previous period metrics
        var previousMetrics = await GetPeriodMetricsAsync(quarryId, previousFromDate, previousToDate);

        // Calculate changes
        var revenueChange = previousMetrics.Revenue > 0
            ? ((currentMetrics.Revenue - previousMetrics.Revenue) / previousMetrics.Revenue) * 100
            : 0;

        var expenseChange = previousMetrics.Expenses > 0
            ? ((currentMetrics.Expenses - previousMetrics.Expenses) / previousMetrics.Expenses) * 100
            : 0;

        var quantityChange = previousMetrics.Quantity > 0
            ? ((currentMetrics.Quantity - previousMetrics.Quantity) / previousMetrics.Quantity) * 100
            : 0;

        var marginChange = currentMetrics.MarginPercent - previousMetrics.MarginPercent;

        return new ComparativePeriodAnalysis
        {
            CurrentPeriod = currentMetrics,
            PreviousPeriod = previousMetrics,
            RevenueChangePercent = revenueChange,
            ExpenseChangePercent = expenseChange,
            QuantityChangePercent = quantityChange,
            MarginChange = marginChange
        };
    }

    /// <summary>
    /// Cash flow waterfall data for visualization
    /// </summary>
    public async Task<CashFlowWaterfallData> GetCashFlowWaterfallAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Get opening balance (only for single-day reports)
        var openingBalance = 0.0;
        if (fromDate.Date == toDate.Date && !string.IsNullOrEmpty(quarryId))
        {
            var previousDate = fromDate.AddDays(-1);
            var previousNote = await context.DailyNotes
                .Where(n => n.quarryId == quarryId)
                .Where(n => n.NoteDate == previousDate)
                .FirstOrDefaultAsync();

            openingBalance = previousNote?.ClosingBalance ?? 0;
        }

        // Get sales
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery.Include(s => s.Product).ToListAsync();

        var totalSales = sales.Sum(s => s.Quantity * s.PricePerUnit);
        var unpaidSales = sales.Where(s => s.PaymentStatus == "NotPaid").Sum(s => s.Quantity * s.PricePerUnit);
        var paidSales = totalSales - unpaidSales;

        // Get expenses breakdown
        var manualExpenses = await context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => string.IsNullOrEmpty(quarryId) || e.QId == quarryId)
            .SumAsync(e => e.Amount);

        // Get quarry for fees
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        var commission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);

        var loadersFee = quarry?.LoadersFee != null
            ? sales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
            : 0;

        var landRateFee = 0.0;
        if (quarry?.LandRateFee != null && quarry.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate if excluded for this sale
                if (!sale.IncludeLandRate)
                    continue;

                var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
                var rate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                landRateFee += sale.Quantity * rate;
            }
        }

        // Get fuel cost
        var fuelAnalysis = await GetFuelCostAnalysisAsync(quarryId, fromDate, toDate);

        // Get banking
        var banked = await context.Bankings
            .Where(b => b.IsActive)
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0)
            .Where(b => string.Compare(b.DateStamp, toStamp) <= 0)
            .Where(b => string.IsNullOrEmpty(quarryId) || b.QId == quarryId)
            .SumAsync(b => b.AmountBanked);

        var totalExpenses = manualExpenses + commission + loadersFee + landRateFee + fuelAnalysis.TotalFuelCost;
        var closingBalance = openingBalance + paidSales - totalExpenses - banked;

        // Build waterfall steps
        var steps = new List<WaterfallStep>();

        if (openingBalance != 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Opening Balance",
                Value = openingBalance,
                Type = WaterfallStepType.Total
            });
        }

        steps.Add(new WaterfallStep
        {
            Label = "Sales Revenue",
            Value = paidSales,
            Type = WaterfallStepType.Positive
        });

        if (manualExpenses > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Manual Expenses",
                Value = -manualExpenses,
                Type = WaterfallStepType.Negative
            });
        }

        if (commission > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Commission",
                Value = -commission,
                Type = WaterfallStepType.Negative
            });
        }

        if (loadersFee > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Loaders Fee",
                Value = -loadersFee,
                Type = WaterfallStepType.Negative
            });
        }

        if (landRateFee > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Land Rate Fee",
                Value = -landRateFee,
                Type = WaterfallStepType.Negative
            });
        }

        if (fuelAnalysis.TotalFuelCost > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Fuel Cost",
                Value = -fuelAnalysis.TotalFuelCost,
                Type = WaterfallStepType.Negative
            });
        }

        if (unpaidSales > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Unpaid Orders",
                Value = -unpaidSales,
                Type = WaterfallStepType.Negative
            });
        }

        if (banked > 0)
        {
            steps.Add(new WaterfallStep
            {
                Label = "Banked",
                Value = -banked,
                Type = WaterfallStepType.Negative
            });
        }

        steps.Add(new WaterfallStep
        {
            Label = "Closing Balance",
            Value = closingBalance,
            Type = WaterfallStepType.Total
        });

        return new CashFlowWaterfallData
        {
            Steps = steps
        };
    }

    /// <summary>
    /// Efficiency metrics (fuel efficiency, capacity utilization, collection rate)
    /// </summary>
    public async Task<EfficiencyMetrics> GetEfficiencyMetricsAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Get fuel efficiency
        var fuelAnalysis = await GetFuelCostAnalysisAsync(quarryId, fromDate, toDate);

        // Get sales for collection rate
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery.ToListAsync();

        var totalSales = sales.Sum(s => s.Quantity * s.PricePerUnit);
        var paidSales = sales.Where(s => s.PaymentStatus == "Paid").Sum(s => s.Quantity * s.PricePerUnit);
        var collectionRate = totalSales > 0 ? (paidSales / totalSales) * 100 : 0;

        // Calculate daily output average
        var dayCount = (toDate - fromDate).Days + 1;
        var totalQuantity = sales.Sum(s => s.Quantity);
        var avgDailyOutput = dayCount > 0 ? totalQuantity / dayCount : 0;

        // Gross margin
        var costBreakdown = await GetCostBreakdownPerPieceAsync(quarryId, fromDate, toDate);
        var grossMarginPercent = costBreakdown.MarginPercent;

        // Commission rate (as percentage of revenue)
        var totalCommission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);
        var commissionRate = totalSales > 0 ? (totalCommission / totalSales) * 100 : 0;

        return new EfficiencyMetrics
        {
            FuelEfficiency = fuelAnalysis.FuelEfficiency,
            CollectionRate = collectionRate,
            AvgDailyOutput = avgDailyOutput,
            GrossMarginPercent = grossMarginPercent,
            CommissionRate = commissionRate
        };
    }

    // Helper method for comparative analysis
    private async Task<PeriodMetrics> GetPeriodMetricsAsync(
        string? quarryId,
        DateTime fromDate,
        DateTime toDate)
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

        var sales = await salesQuery.Include(s => s.Product).ToListAsync();

        var revenue = sales.Sum(s => s.Quantity * s.PricePerUnit);
        var quantity = sales.Sum(s => s.Quantity);

        // Get expenses
        var manualExpenses = await context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => string.IsNullOrEmpty(quarryId) || e.QId == quarryId)
            .SumAsync(e => e.Amount);

        // Get quarry for fees
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        var commission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);

        // Don't apply loaders fee for beam or hardcore products
        var loadersFee = 0.0;
        if (quarry?.LoadersFee != null)
        {
            loadersFee = sales
                .Where(s =>
                {
                    var productName = s.Product?.ProductName ?? "";
                    return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                           !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                })
                .Sum(s => s.Quantity * quarry.LoadersFee.Value);
        }

        var landRateFee = 0.0;
        if (quarry?.LandRateFee != null && quarry.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate if excluded for this sale
                if (!sale.IncludeLandRate)
                    continue;

                var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
                var rate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                landRateFee += sale.Quantity * rate;
            }
        }

        var fuelAnalysis = await GetFuelCostAnalysisAsync(quarryId, fromDate, toDate);
        var expenses = manualExpenses + commission + loadersFee + landRateFee + fuelAnalysis.TotalFuelCost;

        var marginPercent = revenue > 0 ? ((revenue - expenses) / revenue) * 100 : 0;

        return new PeriodMetrics
        {
            FromDate = fromDate,
            ToDate = toDate,
            Revenue = revenue,
            Expenses = expenses,
            Quantity = quantity,
            MarginPercent = marginPercent
        };
    }
}

// Data Models

public class DataAnalyticsResult
{
    public double TotalRevenue { get; set; }
    public double TotalQuantity { get; set; }
    public double TotalExpenses { get; set; }
    public double NetIncome { get; set; }
    public FuelCostAnalysis FuelAnalysis { get; set; } = new();
    public CostBreakdownPerPiece CostBreakdown { get; set; } = new();
    public ProductProfitabilityAnalysis ProductProfitability { get; set; } = new();
    public EfficiencyMetrics Efficiency { get; set; } = new();
    public ComparativePeriodAnalysis? Comparison { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int DayCount { get; set; }
    public bool HasFuelCostData { get; set; }
}

public class FuelCostAnalysis
{
    public double TotalFuelConsumed { get; set; }
    public double FuelCostPerLiter { get; set; }
    public double TotalFuelCost { get; set; }
    public double FuelCostPerPiece { get; set; }
    public double FuelEfficiency { get; set; }
    public List<DailyFuelData> DailyTrend { get; set; } = new();
}

public class DailyFuelData
{
    public DateTime Date { get; set; }
    public double FuelConsumed { get; set; }
    public double FuelCost { get; set; }
    public double Quantity { get; set; }
    public double Efficiency { get; set; }
}

public class CostBreakdownPerPiece
{
    public double TotalCostPerPiece { get; set; }
    public double CommissionPerPiece { get; set; }
    public double LoadersFeePerPiece { get; set; }
    public double LandRatePerPiece { get; set; }
    public double ManualExpensePerPiece { get; set; }
    public double FuelCostPerPiece { get; set; }
    public double RevenuePerPiece { get; set; }
    public double NetMarginPerPiece { get; set; }
    public double MarginPercent { get; set; }
}

public class OperatingCostTrends
{
    public TrendGranularity Granularity { get; set; }
    public List<CostTrendPoint> TrendData { get; set; } = new();
}

public class CostTrendPoint
{
    public DateTime Period { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;
    public double TotalCost { get; set; }
    public double ManualExpenses { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }
    public double FuelCost { get; set; }
    public double Quantity { get; set; }
    public double CostPerPiece { get; set; }
}

public class ProductProfitabilityAnalysis
{
    public List<ProductProfitability> Products { get; set; } = new();
}

public class ProductProfitability
{
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Revenue { get; set; }
    public double TotalCost { get; set; }
    public double NetProfit { get; set; }
    public double MarginPercent { get; set; }
    public double AvgPricePerPiece { get; set; }
    public double AvgCostPerPiece { get; set; }
    public double FuelEfficiency { get; set; }
}

public class ComparativePeriodAnalysis
{
    public PeriodMetrics CurrentPeriod { get; set; } = new();
    public PeriodMetrics PreviousPeriod { get; set; } = new();
    public double RevenueChangePercent { get; set; }
    public double ExpenseChangePercent { get; set; }
    public double QuantityChangePercent { get; set; }
    public double MarginChange { get; set; }
}

public class PeriodMetrics
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public double Revenue { get; set; }
    public double Expenses { get; set; }
    public double Quantity { get; set; }
    public double MarginPercent { get; set; }
}

public class CashFlowWaterfallData
{
    public List<WaterfallStep> Steps { get; set; } = new();
}

public class WaterfallStep
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public WaterfallStepType Type { get; set; }
}

public class EfficiencyMetrics
{
    public double FuelEfficiency { get; set; }
    public double CollectionRate { get; set; }
    public double AvgDailyOutput { get; set; }
    public double GrossMarginPercent { get; set; }
    public double CommissionRate { get; set; }
}

public enum TrendGranularity
{
    Daily,
    Weekly,
    Monthly
}

public enum WaterfallStepType
{
    Positive,
    Negative,
    Total
}

/// <summary>
/// Product bubble data for profitability matrix chart
/// </summary>
public class ProductBubbleData
{
    public string ProductName { get; set; } = string.Empty;
    public double Revenue { get; set; }
    public double MarginPercent { get; set; }
    public double Quantity { get; set; }
}
