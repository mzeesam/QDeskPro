using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Models.AI;
using QDeskPro.Domain.Services.AI;
using QDeskPro.Features.Dashboard.Services;
using QDeskPro.Shared.Extensions;
using System.Text.Json;

namespace QDeskPro.Features.AI.Services;

/// <summary>
/// AI-powered sales analytics service that provides intelligent insights,
/// trend analysis, and recommendations based on sales data
/// </summary>
public interface ISalesAnalyticsService
{
    /// <summary>
    /// Get AI-powered dashboard insights for a quarry
    /// </summary>
    Task<SalesInsightsResult> GetSalesInsightsAsync(string? quarryId, DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Get AI-generated trend analysis
    /// </summary>
    Task<TrendAnalysisResult> GetTrendAnalysisAsync(string? quarryId, DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Get actionable recommendations based on sales patterns
    /// </summary>
    Task<RecommendationsResult> GetRecommendationsAsync(string? quarryId, DateTime fromDate, DateTime toDate);

    /// <summary>
    /// Get quick summary for dashboard widget
    /// </summary>
    Task<QuickInsightsSummary> GetQuickInsightsAsync(string? quarryId);
}

public class SalesAnalyticsService : ISalesAnalyticsService
{
    private readonly IAIProviderFactory _providerFactory;
    private readonly AnalyticsService _analyticsService;
    private readonly AppDbContext _context;
    private readonly AIConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SalesAnalyticsService> _logger;

    public SalesAnalyticsService(
        IAIProviderFactory providerFactory,
        AnalyticsService analyticsService,
        AppDbContext context,
        IOptions<AIConfiguration> config,
        IMemoryCache cache,
        ILogger<SalesAnalyticsService> logger)
    {
        _providerFactory = providerFactory;
        _analyticsService = analyticsService;
        _context = context;
        _config = config.Value;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SalesInsightsResult> GetSalesInsightsAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        var cacheKey = $"ai:insights:{quarryId ?? "all"}:{fromDate:yyyyMMdd}:{toDate:yyyyMMdd}";

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Generating AI insights for quarry {QuarryId} from {From} to {To}",
                quarryId, fromDate, toDate);

            try
            {
                // Gather data for AI analysis
                var stats = await _analyticsService.GetDashboardStatsAsync(quarryId, fromDate, toDate);
                var trends = await _analyticsService.GetSalesTrendsAsync(quarryId, fromDate, toDate);
                var productBreakdown = await _analyticsService.GetProductBreakdownAsync(quarryId, fromDate, toDate);
                var dailyBreakdown = await _analyticsService.GetDailyBreakdownAsync(quarryId, fromDate, toDate);

                // Compare with previous period
                var periodDays = (toDate - fromDate).Days + 1;
                var previousFromDate = fromDate.AddDays(-periodDays);
                var previousToDate = fromDate.AddDays(-1);
                var previousStats = await _analyticsService.GetDashboardStatsAsync(quarryId, previousFromDate, previousToDate);

                // Build context for AI
                var dataContext = BuildDataContext(stats, previousStats, trends, productBreakdown, dailyBreakdown);

                // Generate AI insights
                var insights = await GenerateInsightsAsync(dataContext);

                return new SalesInsightsResult
                {
                    Success = true,
                    GeneratedAt = DateTime.UtcNow,
                    Insights = insights,
                    Stats = stats,
                    RevenueChange = CalculatePercentChange(previousStats.TotalRevenue, stats.TotalRevenue),
                    OrdersChange = CalculatePercentChange(previousStats.TotalOrders, stats.TotalOrders),
                    QuantityChange = CalculatePercentChange(previousStats.TotalQuantity, stats.TotalQuantity),
                    ProfitMarginChange = stats.ProfitMargin - previousStats.ProfitMargin,
                    TopProducts = productBreakdown.Products.Take(5).ToList(),
                    Alerts = GenerateAlerts(stats, previousStats, dailyBreakdown)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating sales insights");
                return new SalesInsightsResult
                {
                    Success = false,
                    Error = ex.Message,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }, TimeSpan.FromMinutes(5)) ?? new SalesInsightsResult { Success = false, Error = "Cache error" };
    }

    public async Task<TrendAnalysisResult> GetTrendAnalysisAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        var cacheKey = $"ai:trends:{quarryId ?? "all"}:{fromDate:yyyyMMdd}:{toDate:yyyyMMdd}";

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Generating AI trend analysis for quarry {QuarryId}", quarryId);

            try
            {
                var trends = await _analyticsService.GetSalesTrendsAsync(quarryId, fromDate, toDate);
                var dailyBreakdown = await _analyticsService.GetDailyBreakdownAsync(quarryId, fromDate, toDate);
                var productBreakdown = await _analyticsService.GetProductBreakdownAsync(quarryId, fromDate, toDate);

                // Build trend context
                var trendContext = BuildTrendContext(trends, dailyBreakdown, productBreakdown);

                // Generate AI trend analysis
                var analysis = await GenerateTrendAnalysisAsync(trendContext);

                // Identify patterns
                var patterns = IdentifyPatterns(dailyBreakdown);

                return new TrendAnalysisResult
                {
                    Success = true,
                    GeneratedAt = DateTime.UtcNow,
                    Analysis = analysis,
                    TrendData = trends,
                    Patterns = patterns,
                    BestDay = dailyBreakdown.OrderByDescending(d => d.Revenue).FirstOrDefault()?.Date,
                    WorstDay = dailyBreakdown.OrderBy(d => d.Revenue).FirstOrDefault()?.Date,
                    AverageDailyRevenue = dailyBreakdown.Average(d => d.Revenue),
                    AverageDailyOrders = dailyBreakdown.Average(d => d.Orders)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating trend analysis");
                return new TrendAnalysisResult
                {
                    Success = false,
                    Error = ex.Message,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }, TimeSpan.FromMinutes(5)) ?? new TrendAnalysisResult { Success = false, Error = "Cache error" };
    }

    public async Task<RecommendationsResult> GetRecommendationsAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        var cacheKey = $"ai:recommendations:{quarryId ?? "all"}:{fromDate:yyyyMMdd}:{toDate:yyyyMMdd}";

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Generating AI recommendations for quarry {QuarryId}", quarryId);

            try
            {
                var stats = await _analyticsService.GetDashboardStatsAsync(quarryId, fromDate, toDate);
                var trends = await _analyticsService.GetSalesTrendsAsync(quarryId, fromDate, toDate);
                var productBreakdown = await _analyticsService.GetProductBreakdownAsync(quarryId, fromDate, toDate);
                var dailyBreakdown = await _analyticsService.GetDailyBreakdownAsync(quarryId, fromDate, toDate);

                // Get unpaid orders
                var unpaidCount = await GetUnpaidOrdersCountAsync(quarryId, fromDate, toDate);

                // Build context for recommendations
                var context = BuildRecommendationContext(stats, productBreakdown, dailyBreakdown, unpaidCount);

                // Generate AI recommendations
                var recommendations = await GenerateRecommendationsAsync(context);

                return new RecommendationsResult
                {
                    Success = true,
                    GeneratedAt = DateTime.UtcNow,
                    Recommendations = recommendations,
                    UnpaidOrdersCount = unpaidCount.Count,
                    UnpaidOrdersAmount = unpaidCount.TotalAmount,
                    TopOpportunity = GetTopOpportunity(stats, productBreakdown),
                    FocusArea = DetermineFocusArea(stats)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations");
                return new RecommendationsResult
                {
                    Success = false,
                    Error = ex.Message,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }, TimeSpan.FromMinutes(5)) ?? new RecommendationsResult { Success = false, Error = "Cache error" };
    }

    public async Task<QuickInsightsSummary> GetQuickInsightsAsync(string? quarryId)
    {
        var cacheKey = $"ai:quick:{quarryId ?? "all"}:{DateTime.Today:yyyyMMdd}";

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            try
            {
                var today = DateTime.Today;
                var weekAgo = today.AddDays(-7);

                var stats = await _analyticsService.GetDashboardStatsAsync(quarryId, weekAgo, today);

                // Compare with previous week
                var twoWeeksAgo = weekAgo.AddDays(-7);
                var previousStats = await _analyticsService.GetDashboardStatsAsync(quarryId, twoWeeksAgo, weekAgo.AddDays(-1));

                var revenueChange = CalculatePercentChange(previousStats.TotalRevenue, stats.TotalRevenue);
                var ordersChange = CalculatePercentChange(previousStats.TotalOrders, stats.TotalOrders);

                // Generate quick insight message
                var insight = GenerateQuickInsight(stats, revenueChange, ordersChange);

                return new QuickInsightsSummary
                {
                    Success = true,
                    GeneratedAt = DateTime.UtcNow,
                    MainInsight = insight,
                    WeeklyRevenue = stats.TotalRevenue,
                    WeeklyOrders = stats.TotalOrders,
                    RevenueChangePercent = revenueChange,
                    OrdersChangePercent = ordersChange,
                    ProfitMargin = stats.ProfitMargin,
                    Status = DetermineStatus(revenueChange, stats.ProfitMargin)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating quick insights");
                return new QuickInsightsSummary
                {
                    Success = false,
                    Error = ex.Message,
                    GeneratedAt = DateTime.UtcNow
                };
            }
        }, TimeSpan.FromMinutes(2)) ?? new QuickInsightsSummary { Success = false, Error = "Cache error" };
    }

    #region Private Helper Methods

    private string BuildDataContext(
        AnalyticsDashboardStats stats,
        AnalyticsDashboardStats previousStats,
        SalesTrendsData trends,
        ProductBreakdownData products,
        List<DailySummary> daily)
    {
        return $"""
            Sales Performance Summary (Last {stats.DateRangeDays} days):
            - Total Revenue: KES {stats.TotalRevenue:N0} (Previous period: KES {previousStats.TotalRevenue:N0})
            - Total Orders: {stats.TotalOrders} (Previous: {previousStats.TotalOrders})
            - Total Quantity: {stats.TotalQuantity:N0} pieces (Previous: {previousStats.TotalQuantity:N0})
            - Net Income: KES {stats.NetIncome:N0} (Previous: KES {previousStats.NetIncome:N0})
            - Profit Margin: {stats.ProfitMargin:F1}% (Previous: {previousStats.ProfitMargin:F1}%)

            Expense Breakdown:
            - Manual Expenses: KES {stats.ManualExpenses:N0}
            - Commission: KES {stats.Commission:N0}
            - Loaders Fee: KES {stats.LoadersFee:N0}
            - Land Rate: KES {stats.LandRateFee:N0}

            Top Products by Revenue:
            {string.Join("\n", products.Products.Take(5).Select(p => $"- {p.ProductName}: {p.Quantity:N0} pcs, KES {p.Revenue:N0}"))}

            Daily Performance (Best to Worst by Revenue):
            - Best Day: {daily.OrderByDescending(d => d.Revenue).FirstOrDefault()?.Date:ddd, MMM d} - KES {daily.Max(d => d.Revenue):N0}
            - Worst Day: {daily.OrderBy(d => d.Revenue).FirstOrDefault()?.Date:ddd, MMM d} - KES {daily.Min(d => d.Revenue):N0}
            - Average Daily: KES {stats.DailyAverageRevenue:N0}

            Fuel Consumption: {stats.TotalFuelConsumed:N0} liters
            """;
    }

    private string BuildTrendContext(SalesTrendsData trends, List<DailySummary> daily, ProductBreakdownData products)
    {
        var maxRevenue = daily.Max(d => d.Revenue);
        var minRevenue = daily.Min(d => d.Revenue);
        var variance = maxRevenue - minRevenue;

        return $"""
            Trend Analysis Data:

            Daily Revenue Pattern:
            {string.Join("\n", daily.Select(d => $"- {d.Date:ddd, MMM d}: KES {d.Revenue:N0}, {d.Orders} orders"))}

            Key Metrics:
            - Revenue Range: KES {minRevenue:N0} to KES {maxRevenue:N0}
            - Variance: KES {variance:N0}
            - Average: KES {daily.Average(d => d.Revenue):N0}
            - Median: KES {daily.OrderBy(d => d.Revenue).Skip(daily.Count / 2).First().Revenue:N0}

            Product Mix:
            {string.Join("\n", products.Products.Select(p => $"- {p.ProductName}: {p.Orders} orders, {p.Quantity:N0} pcs"))}
            """;
    }

    private string BuildRecommendationContext(
        AnalyticsDashboardStats stats,
        ProductBreakdownData products,
        List<DailySummary> daily,
        UnpaidOrdersSummary unpaid)
    {
        return $"""
            Business Context for Recommendations:

            Financial Performance:
            - Revenue: KES {stats.TotalRevenue:N0}
            - Expenses: KES {stats.TotalExpenses:N0}
            - Net Income: KES {stats.NetIncome:N0}
            - Profit Margin: {stats.ProfitMargin:F1}%

            Collections Issue:
            - Unpaid Orders: {unpaid.Count} orders
            - Outstanding Amount: KES {unpaid.TotalAmount:N0}

            Product Performance:
            {string.Join("\n", products.Products.Select(p => $"- {p.ProductName}: Revenue KES {p.Revenue:N0}, {p.Orders} orders"))}

            Operational Metrics:
            - Fuel Consumption: {stats.TotalFuelConsumed:N0} liters
            - Commission Paid: KES {stats.Commission:N0}
            - Loaders Fee: KES {stats.LoadersFee:N0}

            Daily Consistency:
            - Best Day Revenue: KES {daily.Max(d => d.Revenue):N0}
            - Worst Day Revenue: KES {daily.Min(d => d.Revenue):N0}
            - Zero-Sale Days: {daily.Count(d => d.Orders == 0)}
            """;
    }

    private async Task<List<InsightItem>> GenerateInsightsAsync(string context)
    {
        try
        {
            var chatClient = _providerFactory.CreateChatClient();
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage("""
                    You are a business analyst AI for a quarry sales operation in Kenya.
                    Analyze the data and provide 3-5 key insights. Each insight should:
                    1. Have a clear, concise title (max 50 chars)
                    2. Include a brief description (1-2 sentences)
                    3. Be classified as: 'positive', 'negative', 'neutral', or 'warning'
                    4. Be actionable or informative

                    Format each insight as JSON:
                    {"title": "...", "description": "...", "type": "positive|negative|neutral|warning"}

                    Return a JSON array of insights.
                    """),
                ChatMessage.CreateUserMessage($"Analyze this sales data and provide key insights:\n\n{context}")
            };

            var options = new ChatCompletionOptions { MaxOutputTokenCount = 500 };
            var completion = await chatClient.CompleteChatAsync(messages, options);
            var response = completion.Value.Content.FirstOrDefault()?.Text ?? "[]";

            // Parse JSON response
            return ParseInsightsResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI insight generation failed, using fallback");
            return GenerateFallbackInsights();
        }
    }

    private async Task<string> GenerateTrendAnalysisAsync(string context)
    {
        try
        {
            var chatClient = _providerFactory.CreateChatClient();
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage("""
                    You are a business analyst AI for a quarry sales operation.
                    Analyze trends in the data and provide a concise analysis (2-3 paragraphs).
                    Focus on:
                    - Revenue patterns and consistency
                    - Day-of-week effects
                    - Product mix trends
                    - Areas of concern or opportunity

                    Be specific with numbers and percentages where relevant.
                    """),
                ChatMessage.CreateUserMessage($"Analyze these sales trends:\n\n{context}")
            };

            var options = new ChatCompletionOptions { MaxOutputTokenCount = 400 };
            var completion = await chatClient.CompleteChatAsync(messages, options);
            return completion.Value.Content.FirstOrDefault()?.Text ?? "Unable to generate trend analysis.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI trend analysis failed, using fallback");
            return "Sales trends show typical patterns for the period. Consider reviewing daily performance for optimization opportunities.";
        }
    }

    private async Task<List<RecommendationItem>> GenerateRecommendationsAsync(string context)
    {
        try
        {
            var chatClient = _providerFactory.CreateChatClient();
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage("""
                    You are a business advisor AI for a quarry sales operation in Kenya.
                    Based on the data, provide 3-4 actionable recommendations.
                    Each recommendation should:
                    1. Have a clear title (max 50 chars)
                    2. Include specific action steps (1-2 sentences)
                    3. Be classified by priority: 'high', 'medium', 'low'
                    4. Include expected impact if implemented

                    Format each as JSON:
                    {"title": "...", "action": "...", "priority": "high|medium|low", "impact": "..."}

                    Return a JSON array.
                    """),
                ChatMessage.CreateUserMessage($"Provide recommendations based on this data:\n\n{context}")
            };

            var options = new ChatCompletionOptions { MaxOutputTokenCount = 500 };
            var completion = await chatClient.CompleteChatAsync(messages, options);
            var response = completion.Value.Content.FirstOrDefault()?.Text ?? "[]";

            return ParseRecommendationsResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI recommendations failed, using fallback");
            return GenerateFallbackRecommendations();
        }
    }

    private List<InsightItem> ParseInsightsResponse(string response)
    {
        try
        {
            // Extract JSON array from response (handle markdown code blocks)
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..jsonEnd];
                return JsonSerializer.Deserialize<List<InsightItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse insights JSON");
        }
        return GenerateFallbackInsights();
    }

    private List<RecommendationItem> ParseRecommendationsResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..jsonEnd];
                return JsonSerializer.Deserialize<List<RecommendationItem>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse recommendations JSON");
        }
        return GenerateFallbackRecommendations();
    }

    private List<InsightItem> GenerateFallbackInsights() =>
    [
        new InsightItem
        {
            Title = "Data Analysis Available",
            Description = "Your sales data is ready for review. Check the dashboard for detailed metrics.",
            Type = "neutral"
        }
    ];

    private List<RecommendationItem> GenerateFallbackRecommendations() =>
    [
        new RecommendationItem
        {
            Title = "Review Daily Performance",
            Action = "Check daily sales reports to identify patterns and opportunities.",
            Priority = "medium",
            Impact = "Better understanding of business performance"
        }
    ];

    private List<AlertItem> GenerateAlerts(AnalyticsDashboardStats stats, AnalyticsDashboardStats previous, List<DailySummary> daily)
    {
        var alerts = new List<AlertItem>();

        // Check for significant revenue drop
        var revenueChange = CalculatePercentChange(previous.TotalRevenue, stats.TotalRevenue);
        if (revenueChange < -20)
        {
            alerts.Add(new AlertItem
            {
                Title = "Revenue Decline",
                Message = $"Revenue dropped by {Math.Abs(revenueChange):F0}% compared to previous period",
                Severity = "warning"
            });
        }

        // Check profit margin
        if (stats.ProfitMargin < 20)
        {
            alerts.Add(new AlertItem
            {
                Title = "Low Profit Margin",
                Message = $"Profit margin is at {stats.ProfitMargin:F1}%. Consider reviewing expense categories.",
                Severity = "warning"
            });
        }

        // Check for zero-sale days
        var zeroSaleDays = daily.Count(d => d.Orders == 0);
        if (zeroSaleDays > 0)
        {
            alerts.Add(new AlertItem
            {
                Title = "Zero-Sale Days",
                Message = $"There were {zeroSaleDays} days with no sales recorded.",
                Severity = zeroSaleDays > 2 ? "warning" : "info"
            });
        }

        // High fuel consumption alert
        if (stats.TotalFuelConsumed > 0 && stats.TotalRevenue > 0)
        {
            var fuelCostRatio = (stats.TotalFuelConsumed * 150) / stats.TotalRevenue * 100; // Estimate fuel at 150 KES/L
            if (fuelCostRatio > 10)
            {
                alerts.Add(new AlertItem
                {
                    Title = "High Fuel Usage",
                    Message = $"Fuel costs represent approximately {fuelCostRatio:F1}% of revenue",
                    Severity = "info"
                });
            }
        }

        return alerts;
    }

    private List<TrendPattern> IdentifyPatterns(List<DailySummary> daily)
    {
        var patterns = new List<TrendPattern>();

        // Weekend pattern
        var weekendRevenue = daily.Where(d => d.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Average(d => d.Revenue);
        var weekdayRevenue = daily.Where(d => d.Date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday).Average(d => d.Revenue);

        if (weekendRevenue > weekdayRevenue * 1.2)
        {
            patterns.Add(new TrendPattern { Name = "Weekend Surge", Description = "Sales are higher on weekends" });
        }
        else if (weekdayRevenue > weekendRevenue * 1.2)
        {
            patterns.Add(new TrendPattern { Name = "Weekday Focus", Description = "Most sales occur on weekdays" });
        }

        // Growth or decline trend
        var firstHalf = daily.Take(daily.Count / 2).Average(d => d.Revenue);
        var secondHalf = daily.Skip(daily.Count / 2).Average(d => d.Revenue);
        if (secondHalf > firstHalf * 1.1)
        {
            patterns.Add(new TrendPattern { Name = "Upward Trend", Description = "Revenue is trending upward" });
        }
        else if (firstHalf > secondHalf * 1.1)
        {
            patterns.Add(new TrendPattern { Name = "Downward Trend", Description = "Revenue is trending downward" });
        }

        return patterns;
    }

    private async Task<UnpaidOrdersSummary> GetUnpaidOrdersCountAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var query = _context.Sales
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "NotPaid")
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            query = query.Where(s => s.QId == quarryId);
        }

        var unpaidSales = await query.ToListAsync();

        return new UnpaidOrdersSummary
        {
            Count = unpaidSales.Count,
            TotalAmount = unpaidSales.Sum(s => s.GrossSaleAmount)
        };
    }

    private string GetTopOpportunity(AnalyticsDashboardStats stats, ProductBreakdownData products)
    {
        if (stats.ProfitMargin < 30)
        {
            return "Improve profit margins by reviewing expense categories";
        }

        var topProduct = products.Products.FirstOrDefault();
        if (topProduct != null)
        {
            return $"Focus on {topProduct.ProductName} - your best performer";
        }

        return "Optimize daily operations for consistency";
    }

    private string DetermineFocusArea(AnalyticsDashboardStats stats)
    {
        if (stats.ProfitMargin < 20) return "Cost Reduction";
        if (stats.TotalOrders < stats.DateRangeDays * 3) return "Sales Growth";
        if (stats.Commission > stats.TotalRevenue * 0.1) return "Commission Optimization";
        return "Maintain Performance";
    }

    private string GenerateQuickInsight(AnalyticsDashboardStats stats, double revenueChange, double ordersChange)
    {
        if (revenueChange > 10)
            return $"Great week! Revenue is up {revenueChange:F0}% with strong sales performance.";
        if (revenueChange < -10)
            return $"Revenue dropped {Math.Abs(revenueChange):F0}% this week. Review daily patterns for insights.";
        if (stats.ProfitMargin > 40)
            return $"Excellent profit margin at {stats.ProfitMargin:F0}%. Operations are running efficiently.";
        if (stats.ProfitMargin < 20)
            return $"Profit margin is tight at {stats.ProfitMargin:F0}%. Consider reviewing expenses.";

        return $"Steady performance this week with {stats.TotalOrders} orders generating KES {stats.TotalRevenue:N0}.";
    }

    private InsightStatus DetermineStatus(double revenueChange, double profitMargin)
    {
        if (revenueChange > 10 && profitMargin > 30) return InsightStatus.Excellent;
        if (revenueChange > 0 && profitMargin > 20) return InsightStatus.Good;
        if (revenueChange < -10 || profitMargin < 15) return InsightStatus.NeedsAttention;
        return InsightStatus.Stable;
    }

    private static double CalculatePercentChange(double previous, double current)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return ((current - previous) / previous) * 100;
    }

    #endregion
}

#region Result Models

public class SalesInsightsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<InsightItem> Insights { get; set; } = [];
    public AnalyticsDashboardStats? Stats { get; set; }
    public double RevenueChange { get; set; }
    public double OrdersChange { get; set; }
    public double QuantityChange { get; set; }
    public double ProfitMarginChange { get; set; }
    public List<ProductSalesData> TopProducts { get; set; } = [];
    public List<AlertItem> Alerts { get; set; } = [];
}

public class TrendAnalysisResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Analysis { get; set; } = string.Empty;
    public SalesTrendsData? TrendData { get; set; }
    public List<TrendPattern> Patterns { get; set; } = [];
    public DateTime? BestDay { get; set; }
    public DateTime? WorstDay { get; set; }
    public double AverageDailyRevenue { get; set; }
    public double AverageDailyOrders { get; set; }
}

public class RecommendationsResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<RecommendationItem> Recommendations { get; set; } = [];
    public int UnpaidOrdersCount { get; set; }
    public double UnpaidOrdersAmount { get; set; }
    public string TopOpportunity { get; set; } = string.Empty;
    public string FocusArea { get; set; } = string.Empty;
}

public class QuickInsightsSummary
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string MainInsight { get; set; } = string.Empty;
    public double WeeklyRevenue { get; set; }
    public int WeeklyOrders { get; set; }
    public double RevenueChangePercent { get; set; }
    public double OrdersChangePercent { get; set; }
    public double ProfitMargin { get; set; }
    public InsightStatus Status { get; set; }
}

public class InsightItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "neutral"; // positive, negative, neutral, warning
}

public class AlertItem
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // info, warning, error
}

public class RecommendationItem
{
    public string Title { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Priority { get; set; } = "medium"; // high, medium, low
    public string Impact { get; set; } = string.Empty;
}

public class TrendPattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class UnpaidOrdersSummary
{
    public int Count { get; set; }
    public double TotalAmount { get; set; }
}

public enum InsightStatus
{
    Excellent,
    Good,
    Stable,
    NeedsAttention
}

#endregion
