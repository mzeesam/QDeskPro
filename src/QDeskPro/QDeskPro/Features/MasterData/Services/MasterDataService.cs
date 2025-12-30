namespace QDeskPro.Features.MasterData.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Shared.Extensions;

/// <summary>
/// Service for master data operations with quarry-scoped authorization
/// Handles Quarries, Products, Layers, Brokers, and ProductPrices
/// </summary>
public class MasterDataService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MasterDataService> _logger;

    public MasterDataService(AppDbContext context, IMemoryCache cache, ILogger<MasterDataService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    #region Quarries

    /// <summary>
    /// Get all quarries for Administrator
    /// </summary>
    public async Task<List<Quarry>> GetAllQuarriesAsync()
    {
        _logger.LogDebug("Fetching all quarries");

        var quarries = await _context.Quarries
            .Where(q => q.IsActive)
            .Include(q => q.Manager)
            .OrderBy(q => q.QuarryName)
            .ToListAsync();

        _logger.LogDebug("Found {Count} active quarries", quarries.Count);
        return quarries;
    }

    /// <summary>
    /// Get quarries owned by a Manager (Manager Owner only)
    /// </summary>
    public async Task<List<Quarry>> GetQuarriesForManagerAsync(string managerId)
    {
        _logger.LogDebug("Fetching quarries for ManagerId: {ManagerId}", managerId);

        var quarries = await _context.Quarries
            .Where(q => q.ManagerId == managerId)
            .Where(q => q.IsActive)
            .Include(q => q.Manager)
            .OrderBy(q => q.QuarryName)
            .ToListAsync();

        _logger.LogDebug("Found {Count} quarries for ManagerId: {ManagerId}", quarries.Count, managerId);
        return quarries;
    }

    /// <summary>
    /// Get all accessible quarries for a manager (owned + assigned)
    /// This includes quarries owned by Manager Owners AND quarries assigned to Secondary Managers
    /// </summary>
    public async Task<List<Quarry>> GetAccessibleQuarriesForManagerAsync(string managerId)
    {
        _logger.LogDebug("Fetching accessible quarries (owned + assigned) for ManagerId: {ManagerId}", managerId);

        // Get quarries owned by this manager (Manager Owner)
        var ownedQuarries = await _context.Quarries
            .Where(q => q.ManagerId == managerId && q.IsActive)
            .Include(q => q.Manager)
            .ToListAsync();

        // Get quarries assigned to this manager (Secondary Manager or assigned Manager Owner)
        var assignedQuarryIds = await _context.UserQuarries
            .Where(uq => uq.UserId == managerId && uq.IsActive)
            .Select(uq => uq.QuarryId)
            .ToListAsync();

        var assignedQuarries = await _context.Quarries
            .Where(q => assignedQuarryIds.Contains(q.Id) && q.IsActive)
            .Include(q => q.Manager)
            .ToListAsync();

        // Combine and remove duplicates
        var allQuarries = ownedQuarries
            .Concat(assignedQuarries)
            .GroupBy(q => q.Id)
            .Select(g => g.First())
            .OrderBy(q => q.QuarryName)
            .ToList();

        _logger.LogDebug("Found {Count} accessible quarries for ManagerId: {ManagerId} ({OwnedCount} owned + {AssignedCount} assigned)",
            allQuarries.Count, managerId, ownedQuarries.Count, assignedQuarries.Count);

        return allQuarries;
    }

    /// <summary>
    /// Get a specific quarry by ID
    /// </summary>
    public async Task<Quarry?> GetQuarryByIdAsync(string quarryId)
    {
        return await _context.Quarries
            .Where(q => q.Id == quarryId)
            .Where(q => q.IsActive)
            .Include(q => q.Manager)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Verify user has access to quarry (owns it or is assigned)
    /// </summary>
    public async Task<bool> UserHasQuarryAccessAsync(string userId, string quarryId, bool isAdmin = false)
    {
        if (isAdmin) return true;

        // Check ownership
        var isOwner = await _context.Quarries
            .AnyAsync(q => q.Id == quarryId && q.ManagerId == userId && q.IsActive);

        if (isOwner) return true;

        // Check assignment
        return await _context.UserQuarries
            .AnyAsync(uq => uq.UserId == userId && uq.QuarryId == quarryId && uq.IsActive);
    }

    /// <summary>
    /// Create a new quarry (Manager only)
    /// </summary>
    public async Task<(bool Success, string Message, Quarry? Quarry)> CreateQuarryAsync(Quarry quarry, string managerId)
    {
        try
        {
            var validationErrors = ValidateQuarry(quarry);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            quarry.Id = Guid.NewGuid().ToString();
            quarry.ManagerId = managerId; // Set the owner
            quarry.QId = quarry.Id; // Self-referencing for consistency
            quarry.DateStamp = DateTime.Today.ToString("yyyyMMdd");
            quarry.DateCreated = DateTime.UtcNow;
            quarry.CreatedBy = managerId;
            quarry.IsActive = true;

            _context.Quarries.Add(quarry);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Quarry {QuarryName} created by manager {ManagerId}", quarry.QuarryName, managerId);
            return (true, "Quarry created successfully", quarry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating quarry");
            return (false, $"Error creating quarry: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Update an existing quarry
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateQuarryAsync(Quarry quarry, string userId)
    {
        try
        {
            var existing = await _context.Quarries.FindAsync(quarry.Id);
            if (existing == null)
            {
                return (false, "Quarry not found");
            }

            var validationErrors = ValidateQuarry(quarry);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            existing.QuarryName = quarry.QuarryName;
            existing.Location = quarry.Location;
            existing.LoadersFee = quarry.LoadersFee;
            existing.LandRateFee = quarry.LandRateFee;
            existing.RejectsFee = quarry.RejectsFee;
            existing.EmailRecipients = quarry.EmailRecipients;
            existing.DailyReportEnabled = quarry.DailyReportEnabled;
            existing.DailyReportTime = quarry.DailyReportTime;
            // Capital Investment & ROI fields
            existing.InitialCapitalInvestment = quarry.InitialCapitalInvestment;
            existing.OperationsStartDate = quarry.OperationsStartDate;
            existing.EstimatedMonthlyFixedCosts = quarry.EstimatedMonthlyFixedCosts;
            existing.TargetProfitMargin = quarry.TargetProfitMargin;
            existing.DailyProductionCapacity = quarry.DailyProductionCapacity;
            existing.FuelCostPerLiter = quarry.FuelCostPerLiter;
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Quarry updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quarry");
            return (false, $"Error updating quarry: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a quarry (only if no active sales data)
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteQuarryAsync(string quarryId, string userId)
    {
        try
        {
            var quarry = await _context.Quarries.FindAsync(quarryId);
            if (quarry == null)
            {
                return (false, "Quarry not found");
            }

            // Check if quarry has active sales data
            var hasSales = await _context.Sales.AnyAsync(s => s.QId == quarryId && s.IsActive);
            if (hasSales)
            {
                return (false, "Cannot delete quarry with active sales data");
            }

            quarry.IsActive = false;
            quarry.DateModified = DateTime.UtcNow;
            quarry.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            return (true, "Quarry deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting quarry");
            return (false, $"Error deleting quarry: {ex.Message}");
        }
    }

    private List<string> ValidateQuarry(Quarry quarry)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(quarry.QuarryName))
        {
            errors.Add("Quarry name is required");
        }

        if (string.IsNullOrWhiteSpace(quarry.Location))
        {
            errors.Add("Location is required");
        }

        if (quarry.LoadersFee.HasValue && quarry.LoadersFee < 0)
        {
            errors.Add("Loaders fee cannot be negative");
        }

        if (quarry.LandRateFee.HasValue && quarry.LandRateFee < 0)
        {
            errors.Add("Land rate fee cannot be negative");
        }

        if (quarry.RejectsFee.HasValue && quarry.RejectsFee < 0)
        {
            errors.Add("Rejects fee cannot be negative");
        }

        // Capital Investment & ROI field validation
        if (quarry.InitialCapitalInvestment.HasValue && quarry.InitialCapitalInvestment < 0)
        {
            errors.Add("Initial capital investment cannot be negative");
        }

        if (quarry.OperationsStartDate.HasValue && quarry.OperationsStartDate > DateTime.Today)
        {
            errors.Add("Operations start date cannot be in the future");
        }

        if (quarry.EstimatedMonthlyFixedCosts.HasValue && quarry.EstimatedMonthlyFixedCosts < 0)
        {
            errors.Add("Estimated monthly fixed costs cannot be negative");
        }

        if (quarry.TargetProfitMargin.HasValue && (quarry.TargetProfitMargin < 0 || quarry.TargetProfitMargin > 100))
        {
            errors.Add("Target profit margin must be between 0 and 100");
        }

        if (quarry.DailyProductionCapacity.HasValue && quarry.DailyProductionCapacity <= 0)
        {
            errors.Add("Daily production capacity must be greater than zero");
        }

        if (quarry.FuelCostPerLiter.HasValue && quarry.FuelCostPerLiter <= 0)
        {
            errors.Add("Fuel cost per liter must be greater than zero");
        }

        return errors;
    }

    #endregion

    #region Products

    /// <summary>
    /// Get all products (shared across all quarries) - with caching
    /// </summary>
    public async Task<List<Product>> GetAllProductsAsync()
    {
        _logger.LogDebug("Fetching all products (cached)");

        return await _cache.GetOrCreateAsync(CacheKeys.Products, async () =>
        {
            _logger.LogInformation("Cache miss for products. Loading from database");

            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.ProductName)
                .ToListAsync();

            _logger.LogDebug("Loaded {Count} products from database", products.Count);
            return products;
        }, CacheExpirations.MasterData);
    }

    /// <summary>
    /// Get a specific product by ID
    /// </summary>
    public async Task<Product?> GetProductByIdAsync(string productId)
    {
        return await _context.Products
            .Where(p => p.Id == productId)
            .Where(p => p.IsActive)
            .FirstOrDefaultAsync();
    }

    #endregion

    #region Layers

    /// <summary>
    /// Get all layers for a quarry - with caching
    /// </summary>
    public async Task<List<Layer>> GetLayersForQuarryAsync(string quarryId)
    {
        _logger.LogDebug("Fetching layers for QuarryId: {QuarryId} (cached)", quarryId);

        var cacheKey = string.Format(CacheKeys.QuarryLayers, quarryId);

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Cache miss for layers. Loading from database for QuarryId: {QuarryId}", quarryId);

            var layers = await _context.Layers
                .Where(l => l.QId == quarryId)
                .Where(l => l.IsActive)
                .OrderByDescending(l => l.DateStarted)
                .ToListAsync();

            _logger.LogDebug("Loaded {Count} layers for QuarryId: {QuarryId}", layers.Count, quarryId);
            return layers;
        }, CacheExpirations.MasterData);
    }

    /// <summary>
    /// Get a specific layer by ID
    /// </summary>
    public async Task<Layer?> GetLayerByIdAsync(string layerId)
    {
        return await _context.Layers
            .Where(l => l.Id == layerId)
            .Where(l => l.IsActive)
            .Include(l => l.Quarry)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Create a new layer for a quarry
    /// </summary>
    public async Task<(bool Success, string Message, Layer? Layer)> CreateLayerAsync(Layer layer, string userId, string quarryId)
    {
        try
        {
            var validationErrors = ValidateLayer(layer);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            layer.Id = Guid.NewGuid().ToString();
            layer.QId = quarryId;
            layer.QuarryId = quarryId;
            layer.DateStamp = DateTime.Today.ToString("yyyyMMdd");
            layer.DateCreated = DateTime.UtcNow;
            layer.CreatedBy = userId;
            layer.IsActive = true;

            _context.Layers.Add(layer);
            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryLayers, quarryId));

            _logger.LogInformation("Layer {LayerLevel} created for quarry {QuarryId}", layer.LayerLevel, quarryId);
            return (true, "Layer created successfully", layer);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating layer");
            return (false, $"Error creating layer: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Update an existing layer
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateLayerAsync(Layer layer, string userId)
    {
        try
        {
            var existing = await _context.Layers.FindAsync(layer.Id);
            if (existing == null)
            {
                return (false, "Layer not found");
            }

            var validationErrors = ValidateLayer(layer);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            existing.LayerLevel = layer.LayerLevel;
            existing.DateStarted = layer.DateStarted;
            existing.LayerLength = layer.LayerLength;
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryLayers, existing.QId));

            return (true, "Layer updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating layer");
            return (false, $"Error updating layer: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a layer
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteLayerAsync(string layerId, string userId)
    {
        try
        {
            var layer = await _context.Layers.FindAsync(layerId);
            if (layer == null)
            {
                return (false, "Layer not found");
            }

            // Check if layer is used in sales
            var hasSales = await _context.Sales.AnyAsync(s => s.LayerId == layerId && s.IsActive);
            if (hasSales)
            {
                return (false, "Cannot delete layer that is used in sales records");
            }

            layer.IsActive = false;
            layer.DateModified = DateTime.UtcNow;
            layer.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryLayers, layer.QId));

            return (true, "Layer deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting layer");
            return (false, $"Error deleting layer: {ex.Message}");
        }
    }

    private List<string> ValidateLayer(Layer layer)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(layer.LayerLevel))
        {
            errors.Add("Layer level is required");
        }

        if (layer.LayerLength.HasValue && layer.LayerLength < 0)
        {
            errors.Add("Layer length cannot be negative");
        }

        return errors;
    }

    #endregion

    #region Brokers

    /// <summary>
    /// Get all brokers for a quarry - with caching
    /// </summary>
    public async Task<List<Broker>> GetBrokersForQuarryAsync(string quarryId)
    {
        _logger.LogDebug("Fetching brokers for QuarryId: {QuarryId} (cached)", quarryId);

        var cacheKey = string.Format(CacheKeys.QuarryBrokers, quarryId);

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Cache miss for brokers. Loading from database for QuarryId: {QuarryId}", quarryId);

            var brokers = await _context.Brokers
                .Where(b => b.quarryId == quarryId)
                .Where(b => b.IsActive)
                .OrderBy(b => b.BrokerName)
                .ToListAsync();

            _logger.LogDebug("Loaded {Count} brokers for QuarryId: {QuarryId}", brokers.Count, quarryId);
            return brokers;
        }, CacheExpirations.MasterData);
    }

    /// <summary>
    /// Get a specific broker by ID
    /// </summary>
    public async Task<Broker?> GetBrokerByIdAsync(string brokerId)
    {
        return await _context.Brokers
            .Where(b => b.Id == brokerId)
            .Where(b => b.IsActive)
            .Include(b => b.Quarry)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Create a new broker for a quarry
    /// </summary>
    public async Task<(bool Success, string Message, Broker? Broker)> CreateBrokerAsync(Broker broker, string userId, string quarryId)
    {
        try
        {
            var validationErrors = ValidateBroker(broker);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            broker.Id = Guid.NewGuid().ToString();
            broker.QId = quarryId;
            broker.quarryId = quarryId; // lowercase for legacy compatibility
            broker.DateStamp = DateTime.Today.ToString("yyyyMMdd");
            broker.DateCreated = DateTime.UtcNow;
            broker.CreatedBy = userId;
            broker.IsActive = true;

            _context.Brokers.Add(broker);
            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryBrokers, quarryId));

            _logger.LogInformation("Broker {BrokerName} created for quarry {QuarryId}", broker.BrokerName, quarryId);
            return (true, "Broker created successfully", broker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating broker");
            return (false, $"Error creating broker: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Update an existing broker
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateBrokerAsync(Broker broker, string userId)
    {
        try
        {
            var existing = await _context.Brokers.FindAsync(broker.Id);
            if (existing == null)
            {
                return (false, "Broker not found");
            }

            var validationErrors = ValidateBroker(broker);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            existing.BrokerName = broker.BrokerName;
            existing.Phone = broker.Phone;
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryBrokers, existing.QId));

            return (true, "Broker updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating broker");
            return (false, $"Error updating broker: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a broker
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteBrokerAsync(string brokerId, string userId)
    {
        try
        {
            var broker = await _context.Brokers.FindAsync(brokerId);
            if (broker == null)
            {
                return (false, "Broker not found");
            }

            broker.IsActive = false;
            broker.DateModified = DateTime.UtcNow;
            broker.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryBrokers, broker.QId));

            return (true, "Broker deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting broker");
            return (false, $"Error deleting broker: {ex.Message}");
        }
    }

    private List<string> ValidateBroker(Broker broker)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(broker.BrokerName))
        {
            errors.Add("Broker name is required");
        }

        if (string.IsNullOrWhiteSpace(broker.Phone))
        {
            errors.Add("Phone number is required");
        }

        return errors;
    }

    #endregion

    #region Product Prices

    /// <summary>
    /// Get all product prices for a quarry - with caching
    /// </summary>
    public async Task<List<ProductPrice>> GetProductPricesForQuarryAsync(string quarryId)
    {
        _logger.LogDebug("Fetching product prices for QuarryId: {QuarryId} (cached)", quarryId);

        var cacheKey = string.Format(CacheKeys.QuarryProductPrices, quarryId);

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Cache miss for product prices. Loading from database for QuarryId: {QuarryId}", quarryId);

            var prices = await _context.ProductPrices
                .Where(pp => pp.QuarryId == quarryId)
                .Where(pp => pp.IsActive)
                .Include(pp => pp.Product)
                .Include(pp => pp.Quarry)
                .OrderBy(pp => pp.Product.ProductName)
                .ToListAsync();

            _logger.LogDebug("Loaded {Count} product prices for QuarryId: {QuarryId}", prices.Count, quarryId);
            return prices;
        }, CacheExpirations.MasterData);
    }

    /// <summary>
    /// Get a specific product price by ID
    /// </summary>
    public async Task<ProductPrice?> GetProductPriceByIdAsync(string productPriceId)
    {
        return await _context.ProductPrices
            .Where(pp => pp.Id == productPriceId)
            .Where(pp => pp.IsActive)
            .Include(pp => pp.Product)
            .Include(pp => pp.Quarry)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get or create product price for a product/quarry combination
    /// </summary>
    public async Task<ProductPrice?> GetProductPriceAsync(string productId, string quarryId)
    {
        return await _context.ProductPrices
            .Where(pp => pp.ProductId == productId && pp.QuarryId == quarryId)
            .Where(pp => pp.IsActive)
            .Include(pp => pp.Product)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Create or update product price
    /// </summary>
    public async Task<(bool Success, string Message, ProductPrice? ProductPrice)> UpsertProductPriceAsync(
        string productId, string quarryId, double price, string userId)
    {
        try
        {
            if (price <= 0)
            {
                return (false, "Price must be greater than zero", null);
            }

            var existing = await GetProductPriceAsync(productId, quarryId);

            if (existing != null)
            {
                // Update existing price
                existing.Price = price;
                existing.DateModified = DateTime.UtcNow;
                existing.ModifiedBy = userId;

                await _context.SaveChangesAsync();

                // Invalidate cache
                _cache.Remove(string.Format(CacheKeys.QuarryProductPrices, quarryId));

                return (true, "Price updated successfully", existing);
            }
            else
            {
                // Create new price
                var productPrice = new ProductPrice
                {
                    Id = Guid.NewGuid().ToString(),
                    ProductId = productId,
                    QuarryId = quarryId,
                    QId = quarryId,
                    Price = price,
                    DateStamp = DateTime.Today.ToString("yyyyMMdd"),
                    DateCreated = DateTime.UtcNow,
                    CreatedBy = userId,
                    IsActive = true
                };

                _context.ProductPrices.Add(productPrice);
                await _context.SaveChangesAsync();

                // Invalidate cache
                _cache.Remove(string.Format(CacheKeys.QuarryProductPrices, quarryId));

                _logger.LogInformation("Product price created for product {ProductId} at quarry {QuarryId}: {Price}",
                    productId, quarryId, price);

                return (true, "Price created successfully", productPrice);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upserting product price");
            return (false, $"Error saving price: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Delete a product price
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteProductPriceAsync(string productPriceId, string userId)
    {
        try
        {
            var productPrice = await _context.ProductPrices.FindAsync(productPriceId);
            if (productPrice == null)
            {
                return (false, "Product price not found");
            }

            productPrice.IsActive = false;
            productPrice.DateModified = DateTime.UtcNow;
            productPrice.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            // Invalidate cache
            _cache.Remove(string.Format(CacheKeys.QuarryProductPrices, productPrice.QuarryId));

            return (true, "Price deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product price");
            return (false, $"Error deleting price: {ex.Message}");
        }
    }

    #endregion
}
