namespace QDeskPro.Features.Sales.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Reports.Services;

/// <summary>
/// Service for sale operations (CRUD)
/// </summary>
public class SaleService
{
    private readonly AppDbContext _context;
    private readonly ReportService _reportService;
    private readonly ILogger<SaleService> _logger;

    public SaleService(AppDbContext context, ReportService reportService, ILogger<SaleService> logger)
    {
        _context = context;
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new sale with all required audit fields and data manipulation.
    /// Supports prepayment application for fulfillment sales.
    /// Following exact specifications from implementation_plan.md
    /// </summary>
    public async Task<(bool Success, string Message, Sale? Sale)> CreateSaleAsync(
        Sale sale,
        string userId,
        string quarryId,
        string userFullName,
        string? prepaymentId = null,
        double prepaymentAmount = 0)
    {
        try
        {
            // Validation
            var validationErrors = ValidateSale(sale);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            // Set audit fields as per specification
            sale.Id = Guid.NewGuid().ToString();
            sale.DateStamp = sale.SaleDate!.Value.ToString("yyyyMMdd");
            sale.QId = quarryId;
            sale.ApplicationUserId = userId;
            sale.ClerkName = userFullName; // Denormalized for reporting
            sale.DateCreated = DateTime.UtcNow;
            sale.CreatedBy = userId;
            sale.IsActive = true;

            // Handle prepayment application
            if (!string.IsNullOrEmpty(prepaymentId) && prepaymentAmount > 0)
            {
                sale.PrepaymentId = prepaymentId;
                sale.PrepaymentApplied = prepaymentAmount;
                sale.IsPrepaymentSale = true;

                // Adjust payment status based on remaining balance
                var remainingAmount = sale.GrossSaleAmount - prepaymentAmount;

                if (remainingAmount <= 0)
                {
                    // Fully paid by prepayment (or overpaid)
                    sale.PaymentStatus = "Paid";
                    sale.PaymentReceivedDate = sale.SaleDate;
                }
                else
                {
                    // Customer still owes balance - mark as unpaid
                    // They can pay the balance later, which will be tracked via PaymentReceivedDate
                    sale.PaymentStatus = "Not Paid";
                    sale.PaymentReceivedDate = null;
                }
            }
            else
            {
                // No prepayment - existing logic for regular sales
                // Set PaymentReceivedDate based on initial payment status
                // If paid immediately, payment date = sale date
                // If unpaid, payment date is null until payment is collected later
                if (sale.PaymentStatus == "Paid")
                {
                    sale.PaymentReceivedDate = sale.SaleDate;
                }
                else
                {
                    sale.PaymentReceivedDate = null;
                }
            }

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Sale created: {SaleId} | Vehicle: {Vehicle} | Product: {ProductId} | Qty: {Quantity} | Amount: {Amount:N0} | Payment: {PaymentStatus} | Clerk: {ClerkName}",
                sale.Id, sale.VehicleRegistration, sale.ProductId, sale.Quantity, sale.GrossSaleAmount, sale.PaymentStatus, sale.ClerkName);

            // Trigger cascade recalculation for backdated entries
            if (sale.SaleDate!.Value.Date < DateTime.Today)
            {
                await RecalculateClosingBalancesFromDate(sale.SaleDate.Value, quarryId, userId);
            }

            return (true, "Sale recorded successfully", sale);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create sale for vehicle {Vehicle}", sale.VehicleRegistration);
            return (false, $"Error saving sale: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get sales for a clerk (last 5 days)
    /// </summary>
    public async Task<List<Sale>> GetSalesForClerkAsync(string userId)
    {
        var cutoffDate = DateTime.Today.AddDays(-5);

        return await _context.Sales
            .Where(s => s.ApplicationUserId == userId)
            .Where(s => s.SaleDate >= cutoffDate)
            .Where(s => s.IsActive)
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Get sales for a clerk with optional search (all sales, no date constraint)
    /// Used by sales list page to view past sales
    /// </summary>
    public async Task<List<Sale>> GetSalesForClerkAsync(string userId, string? searchText)
    {
        var query = _context.Sales
            .Where(s => s.ApplicationUserId == userId)
            .Where(s => s.IsActive)
            .AsQueryable();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(s => s.VehicleRegistration.Contains(searchText));
        }

        return await query
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Get a single sale by ID with related entities
    /// </summary>
    public async Task<Sale?> GetSaleByIdAsync(string saleId)
    {
        return await _context.Sales
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .Include(s => s.Clerk)
            .FirstOrDefaultAsync(s => s.Id == saleId && s.IsActive);
    }

    /// <summary>
    /// Update an existing sale
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateSaleAsync(Sale sale, string userId)
    {
        try
        {
            var existing = await _context.Sales.FindAsync(sale.Id);
            if (existing == null)
            {
                return (false, "Sale not found");
            }

            // Validation
            var validationErrors = ValidateSale(sale);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            // Update fields
            existing.SaleDate = sale.SaleDate;
            existing.VehicleRegistration = sale.VehicleRegistration;
            existing.ClientName = sale.ClientName;
            existing.ClientPhone = sale.ClientPhone;
            existing.Destination = sale.Destination;
            existing.ProductId = sale.ProductId;
            existing.LayerId = sale.LayerId;
            existing.Quantity = sale.Quantity;
            existing.PricePerUnit = sale.PricePerUnit;
            existing.BrokerId = sale.BrokerId;
            existing.CommissionPerUnit = sale.CommissionPerUnit;
            existing.IncludeLandRate = sale.IncludeLandRate;

            // Track payment received date when status changes
            // This enables collections tracking for previously unpaid orders
            if (sale.PaymentStatus == "Paid" && existing.PaymentStatus != "Paid")
            {
                // Payment just received - use provided date or default to today
                existing.PaymentReceivedDate = sale.PaymentReceivedDate ?? DateTime.Today;
            }
            else if (sale.PaymentStatus != "Paid" && existing.PaymentStatus == "Paid")
            {
                // Reverted to unpaid - clear payment received date
                existing.PaymentReceivedDate = null;
            }
            // If status unchanged and already Paid, keep existing PaymentReceivedDate

            existing.PaymentStatus = sale.PaymentStatus;
            existing.PaymentMode = sale.PaymentMode;
            existing.PaymentReference = sale.PaymentReference;
            existing.DateStamp = sale.SaleDate!.Value.ToString("yyyyMMdd");
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sale updated: {SaleId} | Vehicle: {Vehicle} | Qty: {Quantity} | Amount: {Amount:N0} | Payment: {PaymentStatus} | By: {UserId}",
                sale.Id, existing.VehicleRegistration, existing.Quantity, existing.GrossSaleAmount, existing.PaymentStatus, userId);

            // Trigger cascade recalculation of closing balances
            await RecalculateClosingBalancesFromDate(existing.SaleDate.Value, existing.QId, userId);

            return (true, "Sale updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update sale {SaleId}", sale.Id);
            return (false, $"Error updating sale: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a sale
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteSaleAsync(string saleId, string userId)
    {
        try
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale == null)
            {
                return (false, "Sale not found");
            }

            sale.IsActive = false;
            sale.DateModified = DateTime.UtcNow;
            sale.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Sale deleted: {SaleId} | Vehicle: {Vehicle} | Amount: {Amount:N0} | By: {UserId}",
                saleId, sale.VehicleRegistration, sale.GrossSaleAmount, userId);

            // Trigger cascade recalculation of closing balances
            await RecalculateClosingBalancesFromDate(sale.SaleDate!.Value, sale.QId, userId);

            return (true, "Sale deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete sale {SaleId}", saleId);
            return (false, $"Error deleting sale: {ex.Message}");
        }
    }

    /// <summary>
    /// Get products for dropdown
    /// </summary>
    public async Task<List<Product>> GetProductsAsync()
    {
        return await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProductName)
            .ToListAsync();
    }

    /// <summary>
    /// Get layers for a quarry (last 3, ordered by DateStarted descending)
    /// </summary>
    public async Task<List<Layer>> GetLayersForQuarryAsync(string quarryId)
    {
        return await _context.Layers
            .Where(l => l.QId == quarryId)
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.DateStarted)
            .Take(3)
            .ToListAsync();
    }

    /// <summary>
    /// Get brokers for a quarry
    /// </summary>
    public async Task<List<Broker>> GetBrokersForQuarryAsync(string quarryId)
    {
        return await _context.Brokers
            .Where(b => b.quarryId == quarryId)
            .Where(b => b.IsActive)
            .OrderBy(b => b.BrokerName)
            .ToListAsync();
    }

    /// <summary>
    /// Get product price for a specific product and quarry
    /// </summary>
    public async Task<ProductPrice?> GetProductPriceAsync(string productId, string quarryId)
    {
        return await _context.ProductPrices
            .Where(pp => pp.ProductId == productId)
            .Where(pp => pp.QuarryId == quarryId)
            .Where(pp => pp.IsActive)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get quarry by ID
    /// </summary>
    public async Task<Quarry?> GetQuarryAsync(string quarryId)
    {
        return await _context.Quarries
            .Where(q => q.Id == quarryId)
            .Where(q => q.IsActive)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Validate sale according to exact rules from implementation_plan.md
    /// </summary>
    private List<string> ValidateSale(Sale sale)
    {
        var errors = new List<string>();

        // Vehicle Registration: Required, not empty
        if (string.IsNullOrWhiteSpace(sale.VehicleRegistration))
        {
            errors.Add("Please provide the vehicle registration details");
        }

        // Sale Date: Required
        if (sale.SaleDate == null)
        {
            errors.Add("Sale Date is required");
        }
        else
        {
            // Date validation: Max today, Min 5 days ago
            if (sale.SaleDate.Value.Date < DateTime.Today.AddDays(-5))
            {
                errors.Add("Cannot backdate sale more than 5 days");
            }

            if (sale.SaleDate.Value.Date > DateTime.Today)
            {
                errors.Add("Sale date cannot be in the future");
            }
        }

        // Product: Required
        if (string.IsNullOrWhiteSpace(sale.ProductId))
        {
            errors.Add("Please specify product");
        }

        // Layer: Required
        if (string.IsNullOrWhiteSpace(sale.LayerId))
        {
            errors.Add("Please specify layer");
        }

        // Quantity: Must be > 0
        if (sale.Quantity <= 0)
        {
            errors.Add("Cannot proceed if quantity not provided");
        }

        // Price: Must be > 0
        if (sale.PricePerUnit <= 0)
        {
            errors.Add("Cannot proceed if price not provided");
        }

        // Payment Status: Required
        if (string.IsNullOrWhiteSpace(sale.PaymentStatus))
        {
            errors.Add("Payment Status is required");
        }

        // Payment Mode: Required
        if (string.IsNullOrWhiteSpace(sale.PaymentMode))
        {
            errors.Add("Payment Mode is required");
        }

        return errors;
    }

    /// <summary>
    /// Get all unpaid orders for a quarry (no date limit - show ALL unpaid)
    /// Used by clerk unpaid orders page to track debtors
    /// </summary>
    public async Task<List<Sale>> GetUnpaidOrdersForQuarryAsync(string quarryId, string? searchText = null)
    {
        var query = _context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.PaymentStatus != "Paid")
            .Where(s => s.IsActive)
            .AsQueryable();

        // Apply search filter if provided
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            query = query.Where(s =>
                s.VehicleRegistration.Contains(searchText) ||
                (s.ClientName != null && s.ClientName.Contains(searchText)));
        }

        return await query
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate) // Oldest first
            .ThenByDescending(s => s.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Get summary statistics for unpaid orders
    /// </summary>
    public async Task<UnpaidOrdersSummary> GetUnpaidOrdersSummaryAsync(string quarryId)
    {
        var unpaidSales = await _context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.PaymentStatus != "Paid")
            .Where(s => s.IsActive)
            .Select(s => new { s.GrossSaleAmount, s.SaleDate })
            .ToListAsync();

        var summary = new UnpaidOrdersSummary
        {
            TotalCount = unpaidSales.Count,
            TotalAmount = unpaidSales.Sum(s => s.GrossSaleAmount),
            OldestDays = unpaidSales.Any() && unpaidSales.Min(s => s.SaleDate) != null
                ? (int)(DateTime.Today - unpaidSales.Min(s => s.SaleDate)!.Value.Date).TotalDays
                : 0
        };

        return summary;
    }

    /// <summary>
    /// Recalculate closing balances for all days from startDate to today
    /// This ensures the opening balance chain remains accurate after historical edits
    /// </summary>
    private async Task RecalculateClosingBalancesFromDate(DateTime startDate, string quarryId, string userId)
    {
        var startStamp = startDate.ToString("yyyyMMdd");
        var todayStamp = DateTime.Today.ToString("yyyyMMdd");

        // Get all dates that have DailyNotes and need recalculation (from startDate to today)
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

/// <summary>
/// Summary statistics for unpaid orders
/// </summary>
public class UnpaidOrdersSummary
{
    public int TotalCount { get; set; }
    public double TotalAmount { get; set; }
    public int OldestDays { get; set; }
}
