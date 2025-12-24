using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;

namespace QDeskPro.Domain.Services.AI;

/// <summary>
/// Service to execute sales and expense queries for AI function calling
/// </summary>
public interface ISalesQueryService
{
    Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, string? userQuarryId = null);
}

public class SalesQueryService : ISalesQueryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SalesQueryService> _logger;

    public SalesQueryService(IServiceScopeFactory scopeFactory, ILogger<SalesQueryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string> ExecuteToolAsync(string toolName, JsonElement arguments, string? userQuarryId = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var quarryId = arguments.TryGetProperty("quarry_id", out var qId)
                ? qId.GetString()
                : userQuarryId;

            return toolName switch
            {
                "get_sales_summary" => await GetSalesSummaryAsync(context, arguments, quarryId),
                "get_sales_by_date_range" => await GetSalesByDateRangeAsync(context, arguments, quarryId),
                "get_sales_by_product" => await GetSalesByProductAsync(context, arguments, quarryId),
                "get_sales_by_broker" => await GetSalesByBrokerAsync(context, arguments, quarryId),
                "get_expenses_summary" => await GetExpensesSummaryAsync(context, arguments, quarryId),
                "get_expenses_by_date_range" => await GetExpensesByDateRangeAsync(context, arguments, quarryId),
                "get_expenses_by_category" => await GetExpensesByCategoryAsync(context, arguments, quarryId),
                "get_daily_sales_report" => await GetDailySalesReportAsync(context, arguments, quarryId),
                "get_top_products" => await GetTopProductsAsync(context, arguments, quarryId),
                "get_unpaid_orders" => await GetUnpaidOrdersAsync(context, arguments, quarryId),
                "get_fuel_usage" => await GetFuelUsageAsync(context, arguments, quarryId),
                "get_banking_records" => await GetBankingRecordsAsync(context, arguments, quarryId),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private (DateTime start, DateTime end) GetDateRangeFromPeriod(string period)
    {
        var today = DateTime.Today;
        return period switch
        {
            "today" => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "this_week" => (today.AddDays(-(int)today.DayOfWeek), today),
            "last_week" => (today.AddDays(-(int)today.DayOfWeek - 7), today.AddDays(-(int)today.DayOfWeek - 1)),
            "this_month" => (new DateTime(today.Year, today.Month, 1), today),
            "last_month" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                            new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "this_year" => (new DateTime(today.Year, 1, 1), today),
            "all" => (DateTime.MinValue, today),
            _ => (today, today)
        };
    }

    private async Task<string> GetSalesSummaryAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "today";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);

        var query = context.Sales
            .Where(s => s.IsActive)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(s => s.QId == quarryId);

        var summary = await query
            .GroupBy(s => 1)
            .Select(g => new
            {
                TotalOrders = g.Count(),
                TotalQuantity = g.Sum(s => s.Quantity),
                TotalSales = g.Sum(s => s.Quantity * s.PricePerUnit),
                TotalCommission = g.Sum(s => s.Quantity * s.CommissionPerUnit),
                UnpaidAmount = g.Where(s => s.PaymentStatus == "NotPaid")
                               .Sum(s => s.Quantity * s.PricePerUnit),
                UnpaidCount = g.Count(s => s.PaymentStatus == "NotPaid")
            })
            .FirstOrDefaultAsync();

        var result = new
        {
            period,
            start_date = startDate.ToString("yyyy-MM-dd"),
            end_date = endDate.ToString("yyyy-MM-dd"),
            total_orders = summary?.TotalOrders ?? 0,
            total_quantity = summary?.TotalQuantity ?? 0,
            total_sales = summary?.TotalSales ?? 0,
            total_commission = summary?.TotalCommission ?? 0,
            unpaid_amount = summary?.UnpaidAmount ?? 0,
            unpaid_count = summary?.UnpaidCount ?? 0,
            currency = "KES"
        };

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetSalesByDateRangeAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var startDate = DateTime.Parse(args.GetProperty("start_date").GetString()!);
        var endDate = DateTime.Parse(args.GetProperty("end_date").GetString()!);
        var includeDetails = args.TryGetProperty("include_details", out var details) && details.GetBoolean();

        var query = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(s => s.QId == quarryId);

        if (includeDetails)
        {
            var sales = await query
                .OrderByDescending(s => s.SaleDate)
                .Take(50)
                .Select(s => new
                {
                    date = s.SaleDate,
                    vehicle = s.VehicleRegistration,
                    product = s.Product!.ProductName,
                    quantity = s.Quantity,
                    price_per_unit = s.PricePerUnit,
                    amount = s.Quantity * s.PricePerUnit,
                    payment_status = s.PaymentStatus,
                    payment_mode = s.PaymentMode
                })
                .ToListAsync();

            return JsonSerializer.Serialize(new { sales, count = sales.Count, currency = "KES" });
        }

        var summary = await query
            .GroupBy(s => s.SaleDate!.Value.Date)
            .Select(g => new
            {
                date = g.Key,
                orders = g.Count(),
                quantity = g.Sum(s => s.Quantity),
                total = g.Sum(s => s.Quantity * s.PricePerUnit)
            })
            .OrderByDescending(x => x.date)
            .ToListAsync();

        return JsonSerializer.Serialize(new { daily_summary = summary, currency = "KES" });
    }

    private async Task<string> GetSalesByProductAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "today";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);
        var productName = args.TryGetProperty("product_name", out var pn) ? pn.GetString() : null;

        var query = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(s => s.QId == quarryId);

        if (!string.IsNullOrEmpty(productName))
            query = query.Where(s => s.Product!.ProductName.Contains(productName));

        var products = await query
            .GroupBy(s => s.Product!.ProductName)
            .Select(g => new
            {
                product = g.Key,
                quantity = g.Sum(s => s.Quantity),
                revenue = g.Sum(s => s.Quantity * s.PricePerUnit),
                orders = g.Count(),
                avg_price = g.Average(s => s.PricePerUnit)
            })
            .OrderByDescending(x => x.revenue)
            .ToListAsync();

        return JsonSerializer.Serialize(new { period, products, currency = "KES" });
    }

    private async Task<string> GetSalesByBrokerAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "today";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);
        var brokerName = args.TryGetProperty("broker_name", out var bn) ? bn.GetString() : null;

        var query = context.Sales
            .Include(s => s.Broker)
            .Where(s => s.IsActive && s.BrokerId != null)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(s => s.QId == quarryId);

        if (!string.IsNullOrEmpty(brokerName))
            query = query.Where(s => s.Broker!.BrokerName.Contains(brokerName));

        var brokers = await query
            .GroupBy(s => s.Broker!.BrokerName)
            .Select(g => new
            {
                broker = g.Key,
                quantity = g.Sum(s => s.Quantity),
                revenue = g.Sum(s => s.Quantity * s.PricePerUnit),
                commission = g.Sum(s => s.Quantity * s.CommissionPerUnit),
                orders = g.Count()
            })
            .OrderByDescending(x => x.revenue)
            .ToListAsync();

        return JsonSerializer.Serialize(new { period, brokers, currency = "KES" });
    }

    private async Task<string> GetExpensesSummaryAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "today";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);

        // Get manual expenses
        var expenseQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            expenseQuery = expenseQuery.Where(e => e.QId == quarryId);

        var manualExpenses = await expenseQuery.SumAsync(e => e.Amount);

        // Get auto-calculated expenses from sales
        var salesQuery = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            salesQuery = salesQuery.Where(s => s.QId == quarryId);

        var salesExpenses = await salesQuery
            .GroupBy(s => 1)
            .Select(g => new
            {
                Commission = g.Sum(s => s.Quantity * s.CommissionPerUnit),
                Quantity = g.Sum(s => s.Quantity)
            })
            .FirstOrDefaultAsync();

        // Get quarry fee rates
        var quarry = !string.IsNullOrEmpty(quarryId)
            ? await context.Quarries.FindAsync(quarryId)
            : null;

        var loadersFee = (salesExpenses?.Quantity ?? 0) * (quarry?.LoadersFee ?? 50);
        var landRateFee = (salesExpenses?.Quantity ?? 0) * (quarry?.LandRateFee ?? 10);

        var result = new
        {
            period,
            start_date = startDate.ToString("yyyy-MM-dd"),
            end_date = endDate.ToString("yyyy-MM-dd"),
            manual_expenses = manualExpenses,
            commission = salesExpenses?.Commission ?? 0,
            loaders_fee = loadersFee,
            land_rate_fee = landRateFee,
            total_expenses = manualExpenses + (salesExpenses?.Commission ?? 0) + loadersFee + landRateFee,
            currency = "KES"
        };

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetExpensesByDateRangeAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var startDate = DateTime.Parse(args.GetProperty("start_date").GetString()!);
        var endDate = DateTime.Parse(args.GetProperty("end_date").GetString()!);

        var query = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(e => e.QId == quarryId);

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .Select(e => new
            {
                date = e.ExpenseDate,
                item = e.Item,
                amount = e.Amount,
                category = e.Category,
                reference = e.TxnReference
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new { expenses, total = expenses.Sum(e => e.amount), currency = "KES" });
    }

    private async Task<string> GetExpensesByCategoryAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "today";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);
        var category = args.TryGetProperty("category", out var cat) ? cat.GetString() : null;

        var query = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => e.ExpenseDate >= startDate && e.ExpenseDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(e => e.QId == quarryId);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(e => e.Category == category);

        var categories = await query
            .GroupBy(e => e.Category)
            .Select(g => new
            {
                category = g.Key,
                total = g.Sum(e => e.Amount),
                count = g.Count()
            })
            .OrderByDescending(x => x.total)
            .ToListAsync();

        return JsonSerializer.Serialize(new { period, categories, currency = "KES" });
    }

    private async Task<string> GetDailySalesReportAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var dateStr = args.TryGetProperty("date", out var d) ? d.GetString() : null;
        var reportDate = string.IsNullOrEmpty(dateStr) ? DateTime.Today : DateTime.Parse(dateStr);
        var dateStamp = reportDate.ToString("yyyyMMdd");

        // Get sales
        var salesQuery = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive && s.DateStamp == dateStamp);

        if (!string.IsNullOrEmpty(quarryId))
            salesQuery = salesQuery.Where(s => s.QId == quarryId);

        var sales = await salesQuery.ToListAsync();

        // Get manual expenses
        var expensesQuery = context.Expenses
            .Where(e => e.IsActive && e.DateStamp == dateStamp);

        if (!string.IsNullOrEmpty(quarryId))
            expensesQuery = expensesQuery.Where(e => e.QId == quarryId);

        var expenses = await expensesQuery.SumAsync(e => e.Amount);

        // Get banking
        var bankingQuery = context.Bankings
            .Where(b => b.IsActive && b.DateStamp == dateStamp);

        if (!string.IsNullOrEmpty(quarryId))
            bankingQuery = bankingQuery.Where(b => b.QId == quarryId);

        var banked = await bankingQuery.SumAsync(b => b.AmountBanked);

        // Get fuel usage
        var fuelQuery = context.FuelUsages
            .Where(f => f.IsActive && f.DateStamp == dateStamp);

        if (!string.IsNullOrEmpty(quarryId))
            fuelQuery = fuelQuery.Where(f => f.QId == quarryId);

        var fuel = await fuelQuery.FirstOrDefaultAsync();

        // Get previous day's closing balance
        var prevDateStamp = reportDate.AddDays(-1).ToString("yyyyMMdd");
        var prevNote = await context.DailyNotes
            .Where(n => n.DateStamp == prevDateStamp && n.quarryId == quarryId)
            .FirstOrDefaultAsync();

        // Get quarry for fee rates
        var quarry = !string.IsNullOrEmpty(quarryId)
            ? await context.Quarries.FindAsync(quarryId)
            : null;

        // Calculate totals
        var totalSales = sales.Sum(s => s.Quantity * s.PricePerUnit);
        var totalQuantity = sales.Sum(s => s.Quantity);
        var totalCommission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);
        var totalLoadersFee = totalQuantity * (quarry?.LoadersFee ?? 50);
        var totalLandRate = totalQuantity * (quarry?.LandRateFee ?? 10);
        var totalExpenses = expenses + totalCommission + totalLoadersFee + totalLandRate;
        var unpaidAmount = sales.Where(s => s.PaymentStatus == "NotPaid").Sum(s => s.Quantity * s.PricePerUnit);
        var openingBalance = prevNote?.ClosingBalance ?? 0;
        var earnings = totalSales - totalExpenses;
        var netEarnings = earnings + openingBalance - unpaidAmount;
        var closingBalance = netEarnings - banked;

        var result = new
        {
            date = reportDate.ToString("yyyy-MM-dd"),
            opening_balance = openingBalance,
            sales = new
            {
                total_orders = sales.Count,
                total_quantity = totalQuantity,
                total_amount = totalSales,
                unpaid_count = sales.Count(s => s.PaymentStatus == "NotPaid"),
                unpaid_amount = unpaidAmount
            },
            expenses = new
            {
                manual = expenses,
                commission = totalCommission,
                loaders_fee = totalLoadersFee,
                land_rate = totalLandRate,
                total = totalExpenses
            },
            fuel = fuel != null ? new
            {
                old_stock = fuel.OldStock,
                new_stock = fuel.NewStock,
                machines = fuel.MachinesLoaded,
                wheel_loaders = fuel.WheelLoadersLoaded,
                balance = fuel.OldStock + fuel.NewStock - fuel.MachinesLoaded - fuel.WheelLoadersLoaded
            } : null,
            banking = new { amount_banked = banked },
            summary = new
            {
                earnings,
                net_earnings = netEarnings,
                closing_balance = closingBalance
            },
            currency = "KES"
        };

        return JsonSerializer.Serialize(result);
    }

    private async Task<string> GetTopProductsAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "this_month";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);
        var sortBy = args.TryGetProperty("sort_by", out var sb) ? sb.GetString() : "revenue";
        var limit = args.TryGetProperty("limit", out var lim) ? lim.GetInt32() : 5;

        var query = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive)
            .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(s => s.QId == quarryId);

        var grouped = query
            .GroupBy(s => s.Product!.ProductName)
            .Select(g => new
            {
                product = g.Key,
                quantity = g.Sum(s => s.Quantity),
                revenue = g.Sum(s => s.Quantity * s.PricePerUnit),
                orders = g.Count()
            });

        var products = sortBy == "quantity"
            ? await grouped.OrderByDescending(x => x.quantity).Take(limit).ToListAsync()
            : await grouped.OrderByDescending(x => x.revenue).Take(limit).ToListAsync();

        return JsonSerializer.Serialize(new { period, sort_by = sortBy, products, currency = "KES" });
    }

    private async Task<string> GetUnpaidOrdersAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "all";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);

        var query = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive && s.PaymentStatus == "NotPaid");

        if (period != "all")
            query = query.Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(s => s.QId == quarryId);

        var unpaid = await query
            .OrderByDescending(s => s.SaleDate)
            .Select(s => new
            {
                date = s.SaleDate,
                vehicle = s.VehicleRegistration,
                client = s.ClientName,
                phone = s.ClientPhone,
                product = s.Product!.ProductName,
                quantity = s.Quantity,
                amount = s.Quantity * s.PricePerUnit
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new
        {
            period,
            unpaid_orders = unpaid,
            total_count = unpaid.Count,
            total_amount = unpaid.Sum(x => x.amount),
            currency = "KES"
        });
    }

    private async Task<string> GetFuelUsageAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "this_week";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);

        var query = context.FuelUsages
            .Where(f => f.IsActive)
            .Where(f => f.UsageDate >= startDate && f.UsageDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(f => f.QId == quarryId);

        var usage = await query
            .OrderByDescending(f => f.UsageDate)
            .Select(f => new
            {
                date = f.UsageDate,
                old_stock = f.OldStock,
                new_stock = f.NewStock,
                machines = f.MachinesLoaded,
                wheel_loaders = f.WheelLoadersLoaded,
                balance = f.OldStock + f.NewStock - f.MachinesLoaded - f.WheelLoadersLoaded
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new
        {
            period,
            fuel_usage = usage,
            total_consumed = usage.Sum(x => x.machines + x.wheel_loaders),
            unit = "liters"
        });
    }

    private async Task<string> GetBankingRecordsAsync(AppDbContext context, JsonElement args, string? quarryId)
    {
        var period = args.GetProperty("period").GetString() ?? "this_week";
        var (startDate, endDate) = GetDateRangeFromPeriod(period);

        var query = context.Bankings
            .Where(b => b.IsActive)
            .Where(b => b.BankingDate >= startDate && b.BankingDate <= endDate.AddDays(1));

        if (!string.IsNullOrEmpty(quarryId))
            query = query.Where(b => b.QId == quarryId);

        var records = await query
            .OrderByDescending(b => b.BankingDate)
            .Select(b => new
            {
                date = b.BankingDate,
                item = b.Item,
                amount = b.AmountBanked,
                reference = b.TxnReference
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new
        {
            period,
            banking_records = records,
            total_banked = records.Sum(x => x.amount),
            currency = "KES"
        });
    }
}
