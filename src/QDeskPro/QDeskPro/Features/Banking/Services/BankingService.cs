namespace QDeskPro.Features.Banking.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for banking operations (CRUD)
/// </summary>
public class BankingService
{
    private readonly AppDbContext _context;

    public BankingService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Create a new banking record with all required audit fields
    /// Following exact specifications from implementation_plan.md
    /// </summary>
    public async Task<(bool Success, string Message, Banking? Banking)> CreateBankingAsync(Banking banking, string userId, string quarryId)
    {
        try
        {
            // Validation
            var validationErrors = ValidateBanking(banking);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            // Set audit fields and auto-generate Item as per specification
            banking.Id = Guid.NewGuid().ToString();
            banking.DateStamp = banking.BankingDate!.Value.ToString("yyyyMMdd");
            banking.QId = quarryId;
            banking.ApplicationUserId = userId;

            // Auto-generate Item: "{BankingDate:dd MMM} Daily Banking"
            banking.Item = $"{banking.BankingDate.Value:dd MMM} Daily Banking";

            // Generate short reference code from TxnReference (max 10 chars)
            if (!string.IsNullOrWhiteSpace(banking.TxnReference) && banking.TxnReference.Length > 10)
            {
                banking.RefCode = banking.TxnReference.Substring(0, 10);
            }
            else
            {
                banking.RefCode = banking.TxnReference;
            }

            banking.DateCreated = DateTime.UtcNow;
            banking.CreatedBy = userId;
            banking.IsActive = true;

            _context.Bankings.Add(banking);
            await _context.SaveChangesAsync();

            return (true, "New banking record has been captured!", banking);
        }
        catch (Exception ex)
        {
            return (false, $"Error saving banking record: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get banking records for a clerk (last 14 days)
    /// </summary>
    public async Task<List<Banking>> GetBankingForClerkAsync(string userId)
    {
        var cutoffDate = DateTime.Today.AddDays(-14);

        return await _context.Bankings
            .Where(b => b.ApplicationUserId == userId)
            .Where(b => b.BankingDate >= cutoffDate)
            .Where(b => b.IsActive)
            .OrderByDescending(b => b.BankingDate)
            .ToListAsync();
    }

    /// <summary>
    /// Update an existing banking record
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateBankingAsync(Banking banking, string userId)
    {
        try
        {
            var existing = await _context.Bankings.FindAsync(banking.Id);
            if (existing == null)
            {
                return (false, "Banking record not found");
            }

            // Validation
            var validationErrors = ValidateBanking(banking);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            // Update fields
            existing.BankingDate = banking.BankingDate;
            existing.AmountBanked = banking.AmountBanked;
            existing.TxnReference = banking.TxnReference;
            existing.BalanceBF = banking.BalanceBF;
            existing.DateStamp = banking.BankingDate!.Value.ToString("yyyyMMdd");

            // Re-generate Item and RefCode
            existing.Item = $"{banking.BankingDate.Value:dd MMM} Daily Banking";
            if (!string.IsNullOrWhiteSpace(banking.TxnReference) && banking.TxnReference.Length > 10)
            {
                existing.RefCode = banking.TxnReference.Substring(0, 10);
            }
            else
            {
                existing.RefCode = banking.TxnReference;
            }

            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Banking record has been updated!");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating banking record: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a banking record
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteBankingAsync(string bankingId, string userId)
    {
        try
        {
            var banking = await _context.Bankings.FindAsync(bankingId);
            if (banking == null)
            {
                return (false, "Banking record not found");
            }

            banking.IsActive = false;
            banking.DateModified = DateTime.UtcNow;
            banking.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Banking record deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error deleting banking record: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate banking according to exact rules from implementation_plan.md
    /// </summary>
    private List<string> ValidateBanking(Banking banking)
    {
        var errors = new List<string>();

        // Transaction Reference: Required, not empty
        if (string.IsNullOrWhiteSpace(banking.TxnReference))
        {
            errors.Add("Please provide the reference details");
        }

        // Amount Banked: Must be > 0
        if (banking.AmountBanked <= 0)
        {
            errors.Add("Please provide the amount details");
        }

        // Banking Date: Required
        if (banking.BankingDate == null)
        {
            errors.Add("Banking Date is required");
        }
        else
        {
            // Date validation: Max today, Min 14 days ago
            if (banking.BankingDate < DateTime.Today.AddDays(-14))
            {
                errors.Add("Cannot backdate banking more than 14 days");
            }

            if (banking.BankingDate > DateTime.Today)
            {
                errors.Add("Banking date cannot be in the future");
            }
        }

        return errors;
    }
}
