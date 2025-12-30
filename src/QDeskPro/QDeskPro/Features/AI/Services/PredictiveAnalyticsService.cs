using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;

namespace QDeskPro.Features.AI.Services;

/// <summary>
/// Predictive analytics service for revenue forecasting using linear regression
/// </summary>
public class PredictiveAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PredictiveAnalyticsService> _logger;

    public PredictiveAnalyticsService(
        IServiceScopeFactory scopeFactory,
        ILogger<PredictiveAnalyticsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Forecast revenue for the next N days using linear regression
    /// Based on last 90 days of historical data
    /// </summary>
    public async Task<ForecastResult> ForecastRevenueAsync(string? quarryId, int daysToForecast = 30)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get last 90 days of historical data
        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-90);

        var fromStamp = startDate.ToString("yyyyMMdd");
        var toStamp = endDate.ToString("yyyyMMdd");

        // Query sales with multi-tenant isolation
        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        // Group by date and sum revenue
        var dailyRevenueData = await salesQuery
            .GroupBy(s => s.SaleDate!.Value.Date)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(s => s.Quantity * s.PricePerUnit)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // Check if we have enough data (minimum 14 days)
        if (dailyRevenueData.Count < 14)
        {
            _logger.LogWarning("Insufficient historical data for forecasting. Found {Count} days, need at least 14", dailyRevenueData.Count);

            return new ForecastResult
            {
                Historical = new List<HistoricalPoint>(),
                Forecast = new List<ForecastPoint>(),
                Confidence = 0,
                Method = "Insufficient Data",
                Message = $"Need at least 14 days of historical data. Currently have {dailyRevenueData.Count} days."
            };
        }

        // Prepare data for linear regression
        var xValues = Enumerable.Range(0, dailyRevenueData.Count).Select(i => (double)i).ToList();
        var yValues = dailyRevenueData.Select(d => d.Revenue).ToList();

        // Calculate linear regression coefficients
        var (slope, intercept) = CalculateLinearRegression(xValues, yValues);

        // Generate forecast for next N days
        var forecast = new List<ForecastPoint>();
        var lastIndex = dailyRevenueData.Count;

        for (int i = 0; i < daysToForecast; i++)
        {
            var forecastDate = endDate.AddDays(i + 1);
            var forecastValue = slope * (lastIndex + i) + intercept;

            // Calculate confidence interval (±20% margin)
            var margin = Math.Abs(forecastValue) * 0.2;

            forecast.Add(new ForecastPoint
            {
                Date = forecastDate,
                Value = Math.Max(0, forecastValue),  // Ensure non-negative
                LowerBound = Math.Max(0, forecastValue - margin),
                UpperBound = forecastValue + margin
            });
        }

        // Calculate confidence score based on R-squared
        var rSquared = CalculateRSquared(xValues, yValues, slope, intercept);
        var confidence = Math.Max(0, Math.Min(100, rSquared * 100));

        // Build historical points
        var historical = dailyRevenueData.Select(d => new HistoricalPoint
        {
            Date = d.Date,
            Value = d.Revenue
        }).ToList();

        _logger.LogInformation(
            "Revenue forecast generated. Historical days: {HistoricalDays}, Forecast days: {ForecastDays}, Confidence: {Confidence:F1}%, R²: {RSquared:F3}",
            dailyRevenueData.Count, daysToForecast, confidence, rSquared);

        return new ForecastResult
        {
            Historical = historical,
            Forecast = forecast,
            Confidence = confidence,
            Method = "Linear Regression",
            Message = $"Based on {dailyRevenueData.Count} days of historical data"
        };
    }

    /// <summary>
    /// Calculate linear regression coefficients using least squares method
    /// Returns (slope, intercept)
    /// </summary>
    private (double slope, double intercept) CalculateLinearRegression(List<double> xValues, List<double> yValues)
    {
        if (xValues.Count != yValues.Count || xValues.Count == 0)
        {
            _logger.LogWarning("Invalid data for linear regression calculation");
            return (0, 0);
        }

        var n = xValues.Count;
        var sumX = xValues.Sum();
        var sumY = yValues.Sum();
        var sumXY = xValues.Zip(yValues, (x, y) => x * y).Sum();
        var sumX2 = xValues.Sum(x => x * x);

        // Slope: (n * Σ(xy) - Σx * Σy) / (n * Σ(x²) - (Σx)²)
        var denominator = n * sumX2 - sumX * sumX;

        if (Math.Abs(denominator) < 0.0001)
        {
            _logger.LogWarning("Denominator too close to zero in linear regression calculation");
            return (0, yValues.Average());
        }

        var slope = (n * sumXY - sumX * sumY) / denominator;

        // Intercept: (Σy - slope * Σx) / n
        var intercept = (sumY - slope * sumX) / n;

        return (slope, intercept);
    }

    /// <summary>
    /// Calculate R-squared (coefficient of determination) to measure goodness of fit
    /// Values closer to 1 indicate better fit
    /// </summary>
    private double CalculateRSquared(List<double> xValues, List<double> yValues, double slope, double intercept)
    {
        if (xValues.Count == 0 || yValues.Count == 0)
            return 0;

        var meanY = yValues.Average();

        // Total sum of squares: Σ(y - ȳ)²
        var ssTotal = yValues.Sum(y => Math.Pow(y - meanY, 2));

        if (Math.Abs(ssTotal) < 0.0001)
            return 0;

        // Residual sum of squares: Σ(y - ŷ)²
        var ssResidual = xValues.Zip(yValues, (x, y) =>
        {
            var predicted = slope * x + intercept;
            return Math.Pow(y - predicted, 2);
        }).Sum();

        // R² = 1 - (SS_residual / SS_total)
        var rSquared = 1 - (ssResidual / ssTotal);

        return rSquared;
    }

    /// <summary>
    /// Forecast revenue with exponential smoothing (alternative method)
    /// </summary>
    public async Task<ForecastResult> ForecastRevenueWithSmoothingAsync(
        string? quarryId,
        int daysToForecast = 30,
        double alpha = 0.3)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-60);

        var fromStamp = startDate.ToString("yyyyMMdd");
        var toStamp = endDate.ToString("yyyyMMdd");

        var salesQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var dailyRevenueData = await salesQuery
            .GroupBy(s => s.SaleDate!.Value.Date)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(s => s.Quantity * s.PricePerUnit)
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        if (dailyRevenueData.Count < 7)
        {
            return new ForecastResult
            {
                Historical = new List<HistoricalPoint>(),
                Forecast = new List<ForecastPoint>(),
                Confidence = 0,
                Method = "Insufficient Data",
                Message = "Need at least 7 days of data for exponential smoothing"
            };
        }

        // Calculate exponential smoothing
        var smoothedValues = new List<double>();
        double smoothedValue = dailyRevenueData[0].Revenue; // Initialize with first value

        foreach (var data in dailyRevenueData)
        {
            smoothedValue = alpha * data.Revenue + (1 - alpha) * smoothedValue;
            smoothedValues.Add(smoothedValue);
        }

        // Forecast using last smoothed value
        var lastSmoothed = smoothedValues.Last();
        var forecast = new List<ForecastPoint>();

        for (int i = 0; i < daysToForecast; i++)
        {
            var forecastDate = endDate.AddDays(i + 1);
            var margin = lastSmoothed * 0.25; // ±25% margin

            forecast.Add(new ForecastPoint
            {
                Date = forecastDate,
                Value = Math.Max(0, lastSmoothed),
                LowerBound = Math.Max(0, lastSmoothed - margin),
                UpperBound = lastSmoothed + margin
            });
        }

        var historical = dailyRevenueData.Select(d => new HistoricalPoint
        {
            Date = d.Date,
            Value = d.Revenue
        }).ToList();

        return new ForecastResult
        {
            Historical = historical,
            Forecast = forecast,
            Confidence = 70, // Fixed confidence for exponential smoothing
            Method = "Exponential Smoothing",
            Message = $"Alpha: {alpha:F2}"
        };
    }
}

// Data Models

/// <summary>
/// Forecast result with historical data and predictions
/// </summary>
public class ForecastResult
{
    public List<HistoricalPoint> Historical { get; set; } = new();
    public List<ForecastPoint> Forecast { get; set; } = new();
    public double Confidence { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Historical data point
/// </summary>
public class HistoricalPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// Forecast point with confidence interval
/// </summary>
public class ForecastPoint
{
    public DateTime Date { get; set; }
    public double Value { get; set; }
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }
}
