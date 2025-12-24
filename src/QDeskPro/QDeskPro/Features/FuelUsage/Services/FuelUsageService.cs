namespace QDeskPro.Features.FuelUsage.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for fuel usage operations (CRUD)
/// </summary>
public class FuelUsageService
{
    private readonly AppDbContext _context;

    public FuelUsageService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Create a new fuel usage record with all required audit fields
    /// Following exact specifications from implementation_plan.md
    /// </summary>
    public async Task<(bool Success, string Message, FuelUsage? FuelUsage)> CreateFuelUsageAsync(FuelUsage fuelUsage, string userId, string quarryId)
    {
        try
        {
            // Validation
            var validationErrors = ValidateFuelUsage(fuelUsage);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            // Set audit fields as per specification
            fuelUsage.Id = Guid.NewGuid().ToString();
            fuelUsage.DateStamp = fuelUsage.UsageDate!.Value.ToString("yyyyMMdd");
            fuelUsage.QId = quarryId;
            fuelUsage.ApplicationUserId = userId;
            fuelUsage.DateCreated = DateTime.UtcNow;
            fuelUsage.CreatedBy = userId;
            fuelUsage.IsActive = true;

            _context.FuelUsages.Add(fuelUsage);
            await _context.SaveChangesAsync();

            return (true, "Fuel usage has been captured!", fuelUsage);
        }
        catch (Exception ex)
        {
            return (false, $"Error saving fuel usage: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get fuel usage records for quarry (last 14 days)
    /// Note: Not filtered by user - shared across quarry
    /// </summary>
    public async Task<List<FuelUsage>> GetFuelUsageForQuarryAsync(string quarryId)
    {
        var cutoffDate = DateTime.Today.AddDays(-14);

        return await _context.FuelUsages
            .Where(f => f.QId == quarryId)
            .Where(f => f.UsageDate >= cutoffDate)
            .Where(f => f.IsActive)
            .OrderByDescending(f => f.UsageDate)
            .ToListAsync();
    }

    /// <summary>
    /// Update an existing fuel usage record
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateFuelUsageAsync(FuelUsage fuelUsage, string userId)
    {
        try
        {
            var existing = await _context.FuelUsages.FindAsync(fuelUsage.Id);
            if (existing == null)
            {
                return (false, "Fuel usage record not found");
            }

            // Validation
            var validationErrors = ValidateFuelUsage(fuelUsage);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            // Update fields
            existing.UsageDate = fuelUsage.UsageDate;
            existing.OldStock = fuelUsage.OldStock;
            existing.NewStock = fuelUsage.NewStock;
            existing.MachinesLoaded = fuelUsage.MachinesLoaded;
            existing.WheelLoadersLoaded = fuelUsage.WheelLoadersLoaded;
            existing.DateStamp = fuelUsage.UsageDate!.Value.ToString("yyyyMMdd");
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Fuel usage has been updated!");
        }
        catch (Exception ex)
        {
            return (false, $"Error updating fuel usage: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a fuel usage record
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteFuelUsageAsync(string fuelUsageId, string userId)
    {
        try
        {
            var fuelUsage = await _context.FuelUsages.FindAsync(fuelUsageId);
            if (fuelUsage == null)
            {
                return (false, "Fuel usage record not found");
            }

            fuelUsage.IsActive = false;
            fuelUsage.DateModified = DateTime.UtcNow;
            fuelUsage.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Fuel usage record deleted successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error deleting fuel usage record: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate fuel usage according to exact rules from implementation_plan.md
    /// </summary>
    private List<string> ValidateFuelUsage(FuelUsage fuelUsage)
    {
        var errors = new List<string>();

        // Usage Date: Required
        if (fuelUsage.UsageDate == null)
        {
            errors.Add("Usage Date is required");
        }
        else
        {
            // Date validation: Max today, Min 14 days ago
            if (fuelUsage.UsageDate < DateTime.Today.AddDays(-14))
            {
                errors.Add("Cannot backdate fuel usage more than 14 days");
            }

            if (fuelUsage.UsageDate > DateTime.Today)
            {
                errors.Add("Usage date cannot be in the future");
            }
        }

        // All numeric fields must be >= 0
        if (fuelUsage.OldStock < 0)
        {
            errors.Add("Old Stock cannot be negative");
        }

        if (fuelUsage.NewStock < 0)
        {
            errors.Add("New Stock cannot be negative");
        }

        if (fuelUsage.MachinesLoaded < 0)
        {
            errors.Add("Machines Loaded cannot be negative");
        }

        if (fuelUsage.WheelLoadersLoaded < 0)
        {
            errors.Add("Wheel Loaders Loaded cannot be negative");
        }

        // Check balance isn't negative
        var totalStock = fuelUsage.OldStock + fuelUsage.NewStock;
        var used = fuelUsage.MachinesLoaded + fuelUsage.WheelLoadersLoaded;
        var balance = totalStock - used;

        if (balance < 0)
        {
            errors.Add("Fuel usage exceeds available stock");
        }

        return errors;
    }
}
