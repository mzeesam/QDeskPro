namespace QDeskPro.Features.Banking.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Reports.Services;

/// <summary>
/// Service for banking operations (CRUD)
/// </summary>
public class BankingService
{
    private readonly AppDbContext _context;
    private readonly ReportService _reportService;
    private readonly ILogger<BankingService> _logger;

    public BankingService(AppDbContext context, ReportService reportService, ILogger<BankingService> logger)
    {
        _context = context;
        _reportService = reportService;
        _logger = logger;
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

            // Set audit fields
            banking.Id = Guid.NewGuid().ToString();
            banking.DateStamp = banking.BankingDate!.Value.ToString("yyyyMMdd");
            banking.QId = quarryId;
            banking.ApplicationUserId = userId;

            // Only auto-generate Item if user didn't provide a description
            if (string.IsNullOrWhiteSpace(banking.Item))
            {
                banking.Item = $"{banking.BankingDate.Value:dd MMM} Daily Banking";
            }

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

            _logger.LogInformation("Banking created: {BankingId} | Amount: {Amount:N0} | Ref: {Reference} | Date: {Date:dd/MM/yy} | By: {UserId}",
                banking.Id, banking.AmountBanked, banking.TxnReference, banking.BankingDate, userId);

            // Trigger cascade recalculation for backdated entries
            if (banking.BankingDate!.Value.Date < DateTime.Today)
            {
                await RecalculateClosingBalancesFromDate(banking.BankingDate.Value, quarryId, userId);
            }

            return (true, "New banking record has been captured!", banking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create banking record: {Reference}", banking.TxnReference);
            return (false, $"Error saving banking record: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get banking records for a clerk (last 5 days)
    /// </summary>
    public async Task<List<Banking>> GetBankingForClerkAsync(string userId)
    {
        var cutoffDate = DateTime.Today.AddDays(-5);

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

            // Only auto-generate Item if user didn't provide a description, otherwise use user's input
            if (!string.IsNullOrWhiteSpace(banking.Item))
            {
                existing.Item = banking.Item;
            }
            else if (string.IsNullOrWhiteSpace(existing.Item))
            {
                existing.Item = $"{banking.BankingDate.Value:dd MMM} Daily Banking";
            }
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

            _logger.LogInformation("Banking updated: {BankingId} | Amount: {Amount:N0} | Ref: {Reference} | By: {UserId}",
                banking.Id, existing.AmountBanked, existing.TxnReference, userId);

            // Trigger cascade recalculation for all days from edited date to today
            await RecalculateClosingBalancesFromDate(existing.BankingDate.Value, existing.QId, userId);

            return (true, "Banking record has been updated!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update banking record {BankingId}", banking.Id);
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

            _logger.LogInformation("Banking deleted: {BankingId} | Amount: {Amount:N0} | By: {UserId}",
                bankingId, banking.AmountBanked, userId);

            // Trigger cascade recalculation for all days from deleted banking date to today
            await RecalculateClosingBalancesFromDate(banking.BankingDate!.Value, banking.QId, userId);

            return (true, "Banking record deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete banking record {BankingId}", bankingId);
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
            // Date validation: Max today, Min 5 days ago
            if (banking.BankingDate < DateTime.Today.AddDays(-5))
            {
                errors.Add("Cannot backdate banking more than 5 days");
            }

            if (banking.BankingDate > DateTime.Today)
            {
                errors.Add("Banking date cannot be in the future");
            }
        }

        return errors;
    }

    /// <summary>
    /// Recalculate closing balances for all days from startDate to today
    /// This ensures the opening balance chain remains accurate after editing historical data
    /// </summary>
    private async Task RecalculateClosingBalancesFromDate(DateTime startDate, string quarryId, string userId)
    {
        var startStamp = startDate.ToString("yyyyMMdd");
        var todayStamp = DateTime.Today.ToString("yyyyMMdd");

        // Get all dates that need recalculation (from startDate to today)
        var affectedDates = await _context.DailyNotes
            .Where(n => string.Compare(n.DateStamp, startStamp) >= 0)
            .Where(n => string.Compare(n.DateStamp, todayStamp) <= 0)
            .Where(n => n.QId == quarryId)
            .Where(n => n.IsActive)
            .OrderBy(n => n.DateStamp)
            .Select(n => n.DateStamp)
            .ToListAsync();

        // Recalculate each affected day's report (this auto-updates closing balance)
        foreach (var dateStamp in affectedDates)
        {
            var date = DateTime.ParseExact(dateStamp, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture);

            // Regenerate the report - this will recalculate and save the closing balance
            await _reportService.GenerateClerkReportAsync(date, date, quarryId, userId);
        }
    }
}
