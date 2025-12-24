namespace QDeskPro.Domain.Services;

using QDeskPro.Domain.Entities;

/// <summary>
/// Service for calculating sale-related amounts following exact business logic
/// from claude.md specifications
/// </summary>
public class SaleCalculationService
{
    /// <summary>
    /// Calculate gross sale amount (Quantity × PricePerUnit)
    /// </summary>
    public double CalculateGrossAmount(double quantity, double pricePerUnit)
    {
        return quantity * pricePerUnit;
    }

    /// <summary>
    /// Calculate total commission (Quantity × CommissionPerUnit)
    /// </summary>
    public double CalculateCommission(double quantity, double commissionPerUnit)
    {
        return quantity * commissionPerUnit;
    }

    /// <summary>
    /// Calculate loaders fee (Quantity × Quarry.LoadersFee)
    /// </summary>
    public double CalculateLoadersFee(double quantity, double? loadersFeeRate)
    {
        if (!loadersFeeRate.HasValue || loadersFeeRate.Value <= 0)
            return 0;

        return quantity * loadersFeeRate.Value;
    }

    /// <summary>
    /// Calculate land rate fee with special handling for Reject products
    /// If product contains "reject" AND Quarry.RejectsFee > 0: use RejectsFee
    /// Otherwise: use Quarry.LandRateFee
    /// </summary>
    public double CalculateLandRateFee(double quantity, string productName, double? landRateFee, double? rejectsFee)
    {
        // If no land rate fee configured, return 0
        if (!landRateFee.HasValue || landRateFee.Value <= 0)
            return 0;

        // Special case: Reject products use RejectsFee if available
        if (!string.IsNullOrWhiteSpace(productName) &&
            productName.Contains("reject", StringComparison.OrdinalIgnoreCase))
        {
            return quantity * (rejectsFee ?? landRateFee.Value);
        }

        // Standard case: use LandRateFee
        return quantity * landRateFee.Value;
    }

    /// <summary>
    /// Calculate net amount after deducting all fees
    /// NetAmount = GrossAmount - Commission - LoadersFee - LandRateFee
    /// NetAmount minimum is 0 (no negatives)
    /// </summary>
    public double CalculateNetAmount(double grossAmount, double commission, double loadersFee, double landRateFee)
    {
        var netAmount = grossAmount - commission - loadersFee - landRateFee;
        return Math.Max(0, netAmount); // Ensure no negative values
    }

    /// <summary>
    /// Calculate all amounts for a sale in one call
    /// </summary>
    public SaleCalculationResult CalculateAll(
        double quantity,
        double pricePerUnit,
        double commissionPerUnit,
        string productName,
        double? loadersFeeRate,
        double? landRateFee,
        double? rejectsFee)
    {
        var grossAmount = CalculateGrossAmount(quantity, pricePerUnit);
        var commission = CalculateCommission(quantity, commissionPerUnit);
        var loadersFee = CalculateLoadersFee(quantity, loadersFeeRate);
        var landRate = CalculateLandRateFee(quantity, productName, landRateFee, rejectsFee);
        var netAmount = CalculateNetAmount(grossAmount, commission, loadersFee, landRate);

        return new SaleCalculationResult
        {
            GrossAmount = grossAmount,
            Commission = commission,
            LoadersFee = loadersFee,
            LandRateFee = landRate,
            NetAmount = netAmount
        };
    }
}

/// <summary>
/// Result of sale calculation
/// </summary>
public class SaleCalculationResult
{
    public double GrossAmount { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }
    public double NetAmount { get; set; }
}
