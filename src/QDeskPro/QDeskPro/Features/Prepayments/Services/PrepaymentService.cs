using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

namespace QDeskPro.Features.Prepayments.Services;

/// <summary>
/// Service for managing prepayments (customer deposits/advance payments).
/// Handles CRUD operations and prepayment application logic.
/// </summary>
public class PrepaymentService
{
    private readonly AppDbContext _context;

    public PrepaymentService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a new prepayment record.
    /// </summary>
    public async Task<(bool success, string message, Prepayment? prepayment)> CreatePrepaymentAsync(
        Prepayment prepayment,
        string userId,
        string quarryId,
        string clerkName)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(prepayment.VehicleRegistration))
            return (false, "Vehicle registration is required", null);

        if (prepayment.TotalAmountPaid <= 0)
            return (false, "Prepayment amount must be greater than zero", null);

        if (string.IsNullOrWhiteSpace(prepayment.PaymentMode))
            return (false, "Payment mode is required", null);

        if (prepayment.PrepaymentDate > DateTime.Today)
            return (false, "Prepayment date cannot be in the future", null);

        if (prepayment.PrepaymentDate < DateTime.Today.AddDays(-14))
            return (false, "Cannot backdate prepayment more than 14 days", null);

        // Set audit fields
        prepayment.Id = Guid.NewGuid().ToString();
        prepayment.DateCreated = DateTime.UtcNow;
        prepayment.CreatedBy = userId;
        prepayment.ApplicationUserId = userId;
        prepayment.ClerkName = clerkName;
        prepayment.QId = quarryId;
        prepayment.DateStamp = prepayment.PrepaymentDate.ToString("yyyyMMdd");
        prepayment.IsActive = true;
        prepayment.Status = "Active";
        prepayment.AmountUsed = 0;

        try
        {
            _context.Prepayments.Add(prepayment);
            await _context.SaveChangesAsync();

            return (true, "Prepayment recorded successfully", prepayment);
        }
        catch (Exception ex)
        {
            return (false, $"Error saving prepayment: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Searches for prepayments by vehicle registration.
    /// Returns only active and partial prepayments for the specified quarry.
    /// </summary>
    public async Task<List<Prepayment>> SearchPrepaymentsByVehicleAsync(
        string vehicleReg,
        string quarryId)
    {
        if (string.IsNullOrWhiteSpace(vehicleReg) || vehicleReg.Length < 2)
            return new List<Prepayment>();

        return await _context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Where(p => p.Status != "Fulfilled" && p.Status != "Refunded")
            .Where(p => EF.Functions.Like(p.VehicleRegistration, $"%{vehicleReg}%"))
            .Include(p => p.IntendedProduct)
            .OrderByDescending(p => p.PrepaymentDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets detailed information about a specific prepayment including all fulfillment sales.
    /// </summary>
    public async Task<Prepayment?> GetPrepaymentAsync(string prepaymentId)
    {
        return await _context.Prepayments
            .Include(p => p.IntendedProduct)
            .Include(p => p.FulfillmentSales)
                .ThenInclude(s => s.Product)
            .FirstOrDefaultAsync(p => p.Id == prepaymentId);
    }

    /// <summary>
    /// Applies prepayment amount to a sale (called when creating fulfillment sale).
    /// Updates prepayment status based on remaining balance.
    /// </summary>
    public async Task<(bool success, string message)> ApplyPrepaymentToSaleAsync(
        string prepaymentId,
        string saleId,
        double amountToApply)
    {
        var prepayment = await _context.Prepayments.FindAsync(prepaymentId);
        if (prepayment == null)
            return (false, "Prepayment not found");

        if (amountToApply <= 0)
            return (false, "Amount to apply must be greater than zero");

        if (amountToApply > prepayment.RemainingBalance)
            return (false, $"Amount exceeds remaining balance (KES {prepayment.RemainingBalance:N0})");

        var sale = await _context.Sales.FindAsync(saleId);
        if (sale == null)
            return (false, "Sale not found");

        try
        {
            // Update prepayment
            prepayment.AmountUsed += amountToApply;
            prepayment.DateModified = DateTime.UtcNow;

            // Update status based on remaining balance
            if (prepayment.RemainingBalance == 0)
            {
                prepayment.Status = "Fulfilled";
                prepayment.FullyFulfilledDate = DateTime.UtcNow;
            }
            else if (prepayment.AmountUsed > 0)
            {
                prepayment.Status = "Partial";
            }

            // Link sale to prepayment
            sale.PrepaymentId = prepaymentId;
            sale.PrepaymentApplied = amountToApply;
            sale.IsPrepaymentSale = true;
            sale.DateModified = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return (true, "Prepayment applied successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error applying prepayment: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all active and partial prepayments for a quarry.
    /// Used for display in prepayments list page.
    /// </summary>
    public async Task<List<Prepayment>> GetActivePrepaymentsAsync(string quarryId)
    {
        return await _context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Where(p => p.Status == "Active" || p.Status == "Partial")
            .Include(p => p.IntendedProduct)
            .OrderByDescending(p => p.PrepaymentDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all prepayments for a quarry (for reporting/analytics).
    /// </summary>
    public async Task<List<Prepayment>> GetAllPrepaymentsAsync(string quarryId)
    {
        return await _context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Include(p => p.IntendedProduct)
            .OrderByDescending(p => p.PrepaymentDate)
            .ToListAsync();
    }

    /// <summary>
    /// Gets prepayments for a specific date range (for reports).
    /// </summary>
    public async Task<List<Prepayment>> GetPrepaymentsByDateRangeAsync(
        string quarryId,
        DateTime fromDate,
        DateTime toDate,
        string? userId = null)
    {
        var query = _context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Where(p => p.PrepaymentDate >= fromDate && p.PrepaymentDate <= toDate);

        // Filter by user if specified (for clerk reports)
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(p => p.ApplicationUserId == userId);
        }

        return await query
            .Include(p => p.IntendedProduct)
            .OrderBy(p => p.PrepaymentDate)
            .ToListAsync();
    }

    /// <summary>
    /// Refunds a prepayment (marks as refunded, doesn't delete).
    /// </summary>
    public async Task<(bool success, string message, double refundAmount)> RefundPrepaymentAsync(
        string prepaymentId,
        string userId,
        string reason)
    {
        var prepayment = await _context.Prepayments.FindAsync(prepaymentId);
        if (prepayment == null)
            return (false, "Prepayment not found", 0);

        if (prepayment.Status == "Fulfilled")
            return (false, "Cannot refund fully fulfilled prepayment", 0);

        if (prepayment.Status == "Refunded")
            return (false, "Prepayment has already been refunded", 0);

        var refundAmount = prepayment.RemainingBalance;

        try
        {
            prepayment.Status = "Refunded";
            prepayment.DateModified = DateTime.UtcNow;
            prepayment.ModifiedBy = userId;
            prepayment.Notes = (prepayment.Notes ?? "") + $"\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] Refunded: {reason}";

            await _context.SaveChangesAsync();

            return (true, $"Refunded KES {refundAmount:N0}", refundAmount);
        }
        catch (Exception ex)
        {
            return (false, $"Error refunding prepayment: {ex.Message}", 0);
        }
    }

    /// <summary>
    /// Updates prepayment notes or other editable fields.
    /// </summary>
    public async Task<(bool success, string message)> UpdatePrepaymentAsync(
        Prepayment prepayment,
        string userId)
    {
        var existing = await _context.Prepayments.FindAsync(prepayment.Id);
        if (existing == null)
            return (false, "Prepayment not found");

        try
        {
            // Only allow updating certain fields
            existing.ClientName = prepayment.ClientName;
            existing.ClientPhone = prepayment.ClientPhone;
            existing.Notes = prepayment.Notes;
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Prepayment updated successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating prepayment: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets prepayment summary statistics for a quarry.
    /// </summary>
    public async Task<PrepaymentSummary> GetPrepaymentSummaryAsync(string quarryId)
    {
        var activePrepayments = await _context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Where(p => p.Status == "Active" || p.Status == "Partial")
            .ToListAsync();

        return new PrepaymentSummary
        {
            TotalActiveCount = activePrepayments.Count,
            TotalActivePrepaymentAmount = activePrepayments.Sum(p => p.TotalAmountPaid),
            TotalRemainingBalance = activePrepayments.Sum(p => p.RemainingBalance),
            TotalAmountUsed = activePrepayments.Sum(p => p.AmountUsed)
        };
    }

    /// <summary>
    /// Soft deletes a prepayment (sets IsActive = false).
    /// Only allows deletion if no sales have been fulfilled against it.
    /// </summary>
    public async Task<(bool success, string message)> DeletePrepaymentAsync(string prepaymentId, string userId)
    {
        var prepayment = await _context.Prepayments
            .Include(p => p.FulfillmentSales)
            .FirstOrDefaultAsync(p => p.Id == prepaymentId);

        if (prepayment == null)
            return (false, "Prepayment not found");

        if (prepayment.AmountUsed > 0)
            return (false, "Cannot delete prepayment that has been partially or fully used. Consider refunding instead.");

        try
        {
            prepayment.IsActive = false;
            prepayment.DateModified = DateTime.UtcNow;
            prepayment.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Prepayment deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error deleting prepayment: {ex.Message}");
        }
    }
}

/// <summary>
/// Summary statistics for prepayments.
/// </summary>
public class PrepaymentSummary
{
    public int TotalActiveCount { get; set; }
    public double TotalActivePrepaymentAmount { get; set; }
    public double TotalRemainingBalance { get; set; }
    public double TotalAmountUsed { get; set; }
}
