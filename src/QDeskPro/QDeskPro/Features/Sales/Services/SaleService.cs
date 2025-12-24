namespace QDeskPro.Features.Sales.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for sale operations (CRUD)
/// </summary>
public class SaleService
{
    private readonly AppDbContext _context;

    public SaleService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Create a new sale with all required audit fields and data manipulation
    /// Following exact specifications from implementation_plan.md
    /// </summary>
    public async Task<(bool Success, string Message, Sale? Sale)> CreateSaleAsync(Sale sale, string userId, string quarryId, string userFullName)
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

            _context.Sales.Add(sale);
            await _context.SaveChangesAsync();

            return (true, "Sale recorded successfully", sale);
        }
        catch (Exception ex)
        {
            return (false, $"Error saving sale: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get sales for a clerk (last 14 days)
    /// </summary>
    public async Task<List<Sale>> GetSalesForClerkAsync(string userId)
    {
        var cutoffDate = DateTime.Today.AddDays(-14);

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
            existing.PaymentStatus = sale.PaymentStatus;
            existing.PaymentMode = sale.PaymentMode;
            existing.PaymentReference = sale.PaymentReference;
            existing.DateStamp = sale.SaleDate!.Value.ToString("yyyyMMdd");
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Sale updated successfully");
        }
        catch (Exception ex)
        {
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

            return (true, "Sale deleted successfully");
        }
        catch (Exception ex)
        {
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
            // Date validation: Max today, Min 14 days ago
            if (sale.SaleDate.Value.Date < DateTime.Today.AddDays(-14))
            {
                errors.Add("Cannot backdate sale more than 14 days");
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
}
