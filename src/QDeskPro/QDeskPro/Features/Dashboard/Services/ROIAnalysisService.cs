namespace QDeskPro.Features.Dashboard.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for ROI (Return on Investment) analysis for quarries
/// Calculates investment recovery, break-even, profitability, and efficiency metrics
/// </summary>
public class ROIAnalysisService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ROIAnalysisService> _logger;

    public ROIAnalysisService(IServiceScopeFactory scopeFactory, ILogger<ROIAnalysisService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive ROI analysis for a quarry
    /// </summary>
    public async Task<ROIAnalysisData> GetROIAnalysisAsync(string quarryId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var quarry = await context.Quarries.FindAsync(quarryId);
        if (quarry == null)
        {
            _logger.LogWarning("Quarry not found: {QuarryId}", quarryId);
            return new ROIAnalysisData { HasInvestmentData = false };
        }

        // Check if quarry has capital investment data configured
        if (!quarry.InitialCapitalInvestment.HasValue || quarry.InitialCapitalInvestment <= 0)
        {
            return new ROIAnalysisData
            {
                HasInvestmentData = false,
                QuarryName = quarry.QuarryName
            };
        }

        // Determine date range
        var operationsStart = quarry.OperationsStartDate ?? DateTime.Today.AddYears(-1);
        var analysisFromDate = fromDate ?? operationsStart;
        var analysisToDate = toDate ?? DateTime.Today;

        // Ensure from date is not before operations start
        if (analysisFromDate < operationsStart)
        {
            analysisFromDate = operationsStart;
        }

        var fromStamp = analysisFromDate.ToString("yyyyMMdd");
        var toStamp = analysisToDate.ToString("yyyyMMdd");

        // Get all sales for the quarry
        var sales = await context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Include(s => s.Product)
            .ToListAsync();

        // Get manual expenses
        var expenses = await context.Expenses
            .Where(e => e.QId == quarryId)
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .ToListAsync();

        // Get fuel usage
        var fuelUsages = await context.FuelUsages
            .Where(f => f.QId == quarryId)
            .Where(f => f.IsActive)
            .Where(f => string.Compare(f.DateStamp, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp, toStamp) <= 0)
            .ToListAsync();

        // Calculate totals
        var totalRevenue = sales.Sum(s => s.GrossSaleAmount);
        var totalQuantity = sales.Sum(s => s.Quantity);
        var totalOrders = sales.Count;
        var paidOrders = sales.Count(s => s.PaymentStatus == "Paid");

        // Calculate expenses using 4-source model
        var manualExpenses = expenses.Sum(e => e.Amount);
        var commission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);
        var loadersFee = quarry.LoadersFee.HasValue
            ? sales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
            : 0;

        double landRateFee = 0;
        if (quarry.LandRateFee.HasValue && quarry.LandRateFee.Value > 0)
        {
            foreach (var sale in sales)
            {
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

        // Get Collections (payments received during period for sales made before period)
        var collectionsQuery = context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "Paid")
            .Where(s => s.PaymentReceivedDate >= analysisFromDate && s.PaymentReceivedDate <= analysisToDate)
            .Where(s => s.SaleDate < analysisFromDate);

        var totalCollections = await collectionsQuery.SumAsync(s => s.Quantity * s.PricePerUnit);

        // Get Prepayments (customer deposits received during period)
        var prepaymentsQuery = context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Where(p => p.PrepaymentDate >= analysisFromDate && p.PrepaymentDate <= analysisToDate);

        var totalPrepayments = await prepaymentsQuery.SumAsync(p => p.TotalAmountPaid);

        // Get unpaid orders for period
        var unpaidOrders = sales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);

        var totalExpenses = manualExpenses + commission + loadersFee + landRateFee;

        // Net Profit formula: (Earnings + Collections + Prepayments) - Unpaid Orders
        // Where Earnings = TotalRevenue - TotalExpenses
        var earnings = totalRevenue - totalExpenses;
        var netProfit = (earnings + totalCollections + totalPrepayments) - unpaidOrders;

        // Fuel calculations
        var totalFuelConsumed = fuelUsages.Sum(f => f.MachinesLoaded + f.WheelLoadersLoaded);
        var fuelCost = quarry.FuelCostPerLiter.HasValue
            ? totalFuelConsumed * quarry.FuelCostPerLiter.Value
            : 0;

        // Time calculations
        var operatingDays = (int)(analysisToDate.Date - analysisFromDate.Date).TotalDays + 1;
        var totalOperatingDays = (int)(DateTime.Today - operationsStart.Date).TotalDays + 1;
        var operatingMonths = Math.Max(1, totalOperatingDays / 30.0);

        // Investment metrics
        var investment = quarry.InitialCapitalInvestment.Value;

        // Get cumulative profit since operations start for ROI calculation
        var allTimeSales = await context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.IsActive)
            .Include(s => s.Product)
            .ToListAsync();

        var allTimeExpenses = await context.Expenses
            .Where(e => e.QId == quarryId)
            .Where(e => e.IsActive)
            .ToListAsync();

        var allTimeRevenue = allTimeSales.Sum(s => s.GrossSaleAmount);
        var allTimeManualExpenses = allTimeExpenses.Sum(e => e.Amount);
        var allTimeCommission = allTimeSales.Sum(s => s.Quantity * s.CommissionPerUnit);
        var allTimeLoadersFee = quarry.LoadersFee.HasValue
            ? allTimeSales.Sum(s => s.Quantity * quarry.LoadersFee.Value)
            : 0;

        double allTimeLandRateFee = 0;
        if (quarry.LandRateFee.HasValue && quarry.LandRateFee.Value > 0)
        {
            foreach (var sale in allTimeSales)
            {
                var product = sale.Product;
                if (product?.ProductName?.ToLower().Contains("reject") == true && quarry.RejectsFee.HasValue)
                {
                    allTimeLandRateFee += sale.Quantity * quarry.RejectsFee.Value;
                }
                else
                {
                    allTimeLandRateFee += sale.Quantity * quarry.LandRateFee.Value;
                }
            }
        }

        // Get all-time collections
        var allTimeCollectionsQuery = context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "Paid")
            .Where(s => s.PaymentReceivedDate != null);

        var allTimeCollections = await allTimeCollectionsQuery
            .Where(s => s.SaleDate < s.PaymentReceivedDate)
            .SumAsync(s => s.Quantity * s.PricePerUnit);

        // Get all-time prepayments
        var allTimePrepayments = await context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .SumAsync(p => p.TotalAmountPaid);

        // Get all-time unpaid orders
        var allTimeUnpaid = allTimeSales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);

        var allTimeTotalExpenses = allTimeManualExpenses + allTimeCommission + allTimeLoadersFee + allTimeLandRateFee;

        // Cumulative Net Profit formula: (Earnings + Collections + Prepayments) - Unpaid Orders
        var allTimeEarnings = allTimeRevenue - allTimeTotalExpenses;
        var cumulativeNetProfit = (allTimeEarnings + allTimeCollections + allTimePrepayments) - allTimeUnpaid;

        // ROI Calculations
        var basicROI = investment > 0 ? (cumulativeNetProfit / investment) * 100 : 0;
        var annualizedROI = operatingMonths >= 12
            ? basicROI / (operatingMonths / 12.0)
            : basicROI * (12.0 / operatingMonths);
        var investmentRecoveryPercent = investment > 0 ? (cumulativeNetProfit / investment) * 100 : 0;

        // Payback analysis
        var avgMonthlyProfit = cumulativeNetProfit / operatingMonths;
        var paybackPeriodMonths = avgMonthlyProfit > 0 ? investment / avgMonthlyProfit : double.MaxValue;
        var remainingToRecover = Math.Max(0, investment - cumulativeNetProfit);
        DateTime? estimatedRecoveryDate = null;

        if (avgMonthlyProfit > 0 && remainingToRecover > 0)
        {
            var monthsToRecover = remainingToRecover / avgMonthlyProfit;
            estimatedRecoveryDate = DateTime.Today.AddMonths((int)Math.Ceiling(monthsToRecover));
        }
        else if (cumulativeNetProfit >= investment)
        {
            estimatedRecoveryDate = DateTime.Today; // Already recovered
        }

        // Profitability metrics
        var grossProfitMargin = totalRevenue > 0 ? ((totalRevenue - totalExpenses) / totalRevenue) * 100 : 0;
        var netProfitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;
        var revenuePerPiece = totalQuantity > 0 ? totalRevenue / totalQuantity : 0;
        var costPerPiece = totalQuantity > 0 ? totalExpenses / totalQuantity : 0;
        var profitPerPiece = revenuePerPiece - costPerPiece;

        // Efficiency metrics
        var fuelEfficiency = totalFuelConsumed > 0 ? totalQuantity / totalFuelConsumed : 0;
        var capacityUtilization = quarry.DailyProductionCapacity.HasValue && quarry.DailyProductionCapacity > 0
            ? ((totalQuantity / operatingDays) / quarry.DailyProductionCapacity.Value) * 100
            : 0;
        var commissionRatio = totalRevenue > 0 ? (commission / totalRevenue) * 100 : 0;
        var collectionEfficiency = totalOrders > 0 ? ((double)paidOrders / totalOrders) * 100 : 0;

        // Break-even analysis
        var breakEven = CalculateBreakEven(quarry, totalQuantity, operatingDays, totalRevenue, totalExpenses);

        // Monthly trends
        var monthlyHistory = await GetMonthlyTrendsAsync(quarryId, operationsStart, analysisToDate);

        var result = new ROIAnalysisData
        {
            HasInvestmentData = true,
            QuarryName = quarry.QuarryName,

            // Investment info
            TotalInvestment = investment,
            OperationsStartDate = operationsStart,
            OperatingDays = totalOperatingDays,
            OperatingMonths = (int)Math.Round(operatingMonths),

            // ROI metrics
            BasicROI = basicROI,
            AnnualizedROI = annualizedROI,
            InvestmentRecoveryPercent = Math.Min(100, investmentRecoveryPercent),
            CumulativeNetProfit = cumulativeNetProfit,
            PaybackPeriodMonths = paybackPeriodMonths,
            EstimatedRecoveryDate = estimatedRecoveryDate,
            RemainingToRecover = remainingToRecover,

            // Profitability
            GrossProfitMargin = grossProfitMargin,
            NetProfitMargin = netProfitMargin,
            RevenuePerPiece = revenuePerPiece,
            CostPerPiece = costPerPiece,
            ProfitPerPiece = profitPerPiece,

            // Efficiency
            FuelEfficiency = fuelEfficiency,
            CapacityUtilization = capacityUtilization,
            CommissionRatio = commissionRatio,
            CollectionEfficiency = collectionEfficiency,

            // Break-even
            BreakEven = breakEven,

            // Period totals (for selected date range)
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetProfit = netProfit,
            TotalQuantity = totalQuantity,
            TotalOrders = totalOrders,
            TotalFuelConsumed = totalFuelConsumed,

            // Expense breakdown
            ManualExpenses = manualExpenses,
            Commission = commission,
            LoadersFee = loadersFee,
            LandRateFee = landRateFee,

            // Monthly history for charts
            MonthlyHistory = monthlyHistory,

            // Target comparison
            TargetProfitMargin = quarry.TargetProfitMargin,
            IsAboveTarget = quarry.TargetProfitMargin.HasValue && netProfitMargin >= quarry.TargetProfitMargin.Value
        };

        return result;
    }

    /// <summary>
    /// Calculate break-even analysis
    /// </summary>
    private BreakEvenAnalysis CalculateBreakEven(Quarry quarry, double totalQuantity, int operatingDays, double totalRevenue, double totalExpenses)
    {
        var fixedCosts = quarry.EstimatedMonthlyFixedCosts ?? 0;
        var avgQuantityPerMonth = operatingDays > 0 ? (totalQuantity / operatingDays) * 30 : 0;
        var avgPricePerUnit = totalQuantity > 0 ? totalRevenue / totalQuantity : 0;

        // Variable cost per unit (expenses that scale with quantity)
        // This includes commission, loaders fee, land rate (all per-piece costs)
        double variableCostPerUnit = 0;
        if (quarry.LoadersFee.HasValue)
            variableCostPerUnit += quarry.LoadersFee.Value;
        if (quarry.LandRateFee.HasValue)
            variableCostPerUnit += quarry.LandRateFee.Value;

        // Contribution margin per unit
        var contributionMargin = avgPricePerUnit - variableCostPerUnit;

        // Break-even calculations
        var breakEvenPieces = contributionMargin > 0 ? fixedCosts / contributionMargin : 0;
        var breakEvenRevenue = breakEvenPieces * avgPricePerUnit;

        // Margin of safety
        var marginOfSafetyPercent = avgQuantityPerMonth > 0 && avgQuantityPerMonth > breakEvenPieces
            ? ((avgQuantityPerMonth - breakEvenPieces) / avgQuantityPerMonth) * 100
            : 0;

        return new BreakEvenAnalysis
        {
            FixedCosts = fixedCosts,
            VariableCostPerUnit = variableCostPerUnit,
            AveragePricePerUnit = avgPricePerUnit,
            ContributionMargin = contributionMargin,
            BreakEvenPieces = breakEvenPieces,
            BreakEvenRevenue = breakEvenRevenue,
            CurrentMonthlyPieces = avgQuantityPerMonth,
            MarginOfSafetyPercent = marginOfSafetyPercent,
            IsAboveBreakEven = avgQuantityPerMonth >= breakEvenPieces
        };
    }

    /// <summary>
    /// Get monthly profit trends since operations start
    /// </summary>
    public async Task<List<MonthlyProfitData>> GetMonthlyTrendsAsync(string quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var quarry = await context.Quarries.FindAsync(quarryId);
        if (quarry == null) return new List<MonthlyProfitData>();

        var investment = quarry.InitialCapitalInvestment ?? 0;

        // Get all sales grouped by month
        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var sales = await context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Include(s => s.Product)
            .ToListAsync();

        var expenses = await context.Expenses
            .Where(e => e.QId == quarryId)
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .ToListAsync();

        // Group by month
        var salesByMonth = sales
            .Where(s => s.SaleDate.HasValue)
            .GroupBy(s => new { s.SaleDate!.Value.Year, s.SaleDate!.Value.Month })
            .ToDictionary(g => g.Key, g => g.ToList());

        var expensesByMonth = expenses
            .Where(e => e.ExpenseDate.HasValue)
            .GroupBy(e => new { e.ExpenseDate!.Value.Year, e.ExpenseDate!.Value.Month })
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var monthlyData = new List<MonthlyProfitData>();
        var cumulativeProfit = 0.0;

        var currentDate = new DateTime(fromDate.Year, fromDate.Month, 1);
        var endDate = new DateTime(toDate.Year, toDate.Month, 1);

        while (currentDate <= endDate)
        {
            var key = new { Year = currentDate.Year, Month = currentDate.Month };
            var monthSales = salesByMonth.ContainsKey(key) ? salesByMonth[key] : new List<Sale>();
            var manualExpense = expensesByMonth.ContainsKey(key) ? expensesByMonth[key] : 0;

            var revenue = monthSales.Sum(s => s.GrossSaleAmount);
            var quantity = monthSales.Sum(s => s.Quantity);
            var commission = monthSales.Sum(s => s.Quantity * s.CommissionPerUnit);
            var loadersFee = quarry.LoadersFee.HasValue ? quantity * quarry.LoadersFee.Value : 0;

            double landRateFee = 0;
            if (quarry.LandRateFee.HasValue && quarry.LandRateFee.Value > 0)
            {
                foreach (var sale in monthSales)
                {
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

            // Get collections for this month (payments received this month for sales before this month)
            var monthStart = new DateTime(currentDate.Year, currentDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var monthCollections = await context.Sales
                .Where(s => s.QId == quarryId)
                .Where(s => s.IsActive)
                .Where(s => s.PaymentStatus == "Paid")
                .Where(s => s.PaymentReceivedDate >= monthStart && s.PaymentReceivedDate <= monthEnd)
                .Where(s => s.SaleDate < monthStart)
                .SumAsync(s => s.Quantity * s.PricePerUnit);

            // Get prepayments for this month
            var monthPrepayments = await context.Prepayments
                .Where(p => p.QId == quarryId)
                .Where(p => p.IsActive)
                .Where(p => p.PrepaymentDate >= monthStart && p.PrepaymentDate <= monthEnd)
                .SumAsync(p => p.TotalAmountPaid);

            // Get unpaid orders for this month
            var monthUnpaid = monthSales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);

            var totalExpenses = manualExpense + commission + loadersFee + landRateFee;

            // Net Profit formula: (Earnings + Collections + Prepayments) - Unpaid Orders
            var earnings = revenue - totalExpenses;
            var netProfit = (earnings + monthCollections + monthPrepayments) - monthUnpaid;
            cumulativeProfit += netProfit;

            var roi = investment > 0 ? (cumulativeProfit / investment) * 100 : 0;

            monthlyData.Add(new MonthlyProfitData
            {
                Year = currentDate.Year,
                Month = currentDate.Month,
                MonthName = currentDate.ToString("MMM yyyy"),
                Revenue = revenue,
                Expenses = totalExpenses,
                NetProfit = netProfit,
                CumulativeProfit = cumulativeProfit,
                ROI = roi,
                Quantity = quantity
            });

            currentDate = currentDate.AddMonths(1);
        }

        return monthlyData;
    }
}

#region Data Models

/// <summary>
/// Comprehensive ROI analysis data
/// </summary>
public class ROIAnalysisData
{
    public bool HasInvestmentData { get; set; }
    public string QuarryName { get; set; } = string.Empty;

    // Investment Info
    public double TotalInvestment { get; set; }
    public DateTime OperationsStartDate { get; set; }
    public int OperatingDays { get; set; }
    public int OperatingMonths { get; set; }

    // Core ROI Metrics
    public double BasicROI { get; set; }
    public double AnnualizedROI { get; set; }
    public double InvestmentRecoveryPercent { get; set; }
    public double CumulativeNetProfit { get; set; }
    public double PaybackPeriodMonths { get; set; }
    public DateTime? EstimatedRecoveryDate { get; set; }
    public double RemainingToRecover { get; set; }

    // Profitability Metrics
    public double GrossProfitMargin { get; set; }
    public double NetProfitMargin { get; set; }
    public double RevenuePerPiece { get; set; }
    public double CostPerPiece { get; set; }
    public double ProfitPerPiece { get; set; }

    // Efficiency Metrics
    public double FuelEfficiency { get; set; }
    public double CapacityUtilization { get; set; }
    public double CommissionRatio { get; set; }
    public double CollectionEfficiency { get; set; }

    // Break-Even Analysis
    public BreakEvenAnalysis BreakEven { get; set; } = new();

    // Period Totals
    public double TotalRevenue { get; set; }
    public double TotalExpenses { get; set; }
    public double NetProfit { get; set; }
    public double TotalQuantity { get; set; }
    public int TotalOrders { get; set; }
    public double TotalFuelConsumed { get; set; }

    // Expense Breakdown
    public double ManualExpenses { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }

    // Target Comparison
    public double? TargetProfitMargin { get; set; }
    public bool IsAboveTarget { get; set; }

    // Monthly History for Charts
    public List<MonthlyProfitData> MonthlyHistory { get; set; } = new();
}

/// <summary>
/// Monthly profit data for trend charts
/// </summary>
public class MonthlyProfitData
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public double Revenue { get; set; }
    public double Expenses { get; set; }
    public double NetProfit { get; set; }
    public double CumulativeProfit { get; set; }
    public double ROI { get; set; }
    public double Quantity { get; set; }
}

/// <summary>
/// Break-even analysis data
/// </summary>
public class BreakEvenAnalysis
{
    public double FixedCosts { get; set; }
    public double VariableCostPerUnit { get; set; }
    public double AveragePricePerUnit { get; set; }
    public double ContributionMargin { get; set; }
    public double BreakEvenPieces { get; set; }
    public double BreakEvenRevenue { get; set; }
    public double CurrentMonthlyPieces { get; set; }
    public double MarginOfSafetyPercent { get; set; }
    public bool IsAboveBreakEven { get; set; }
}

#endregion
