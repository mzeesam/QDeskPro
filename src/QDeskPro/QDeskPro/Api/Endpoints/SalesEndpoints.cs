using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Sales.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        group.MapGet("", GetSales)
            .WithName("GetSales")
            .WithDescription("Get sales with pagination and filtering");

        group.MapGet("{id}", GetSaleById)
            .WithName("GetSaleById")
            .WithDescription("Get sale details by ID");

        group.MapPost("", CreateSale)
            .WithName("CreateSale")
            .WithDescription("Create a new sale");

        group.MapPut("{id}", UpdateSale)
            .WithName("UpdateSale")
            .WithDescription("Update an existing sale");

        group.MapDelete("{id}", DeleteSale)
            .WithName("DeleteSale")
            .WithDescription("Soft delete a sale");

        // Commented out until implemented in SaleService
        // group.MapGet("daily-summary", GetDailySummary)
        //     .WithName("GetDailySummary")
        //     .WithDescription("Get daily sales summary for date range");

        group.MapGet("by-product", GetSalesByProduct)
            .WithName("GetSalesByProduct")
            .WithDescription("Get sales grouped by product");
    }

    private static async Task<IResult> GetSales(
        ClaimsPrincipal user,
        AppDbContext context,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? quarryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var query = context.Sales
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .Include(s => s.Clerk)
            .Where(s => s.IsActive);

        // Apply role-based filtering
        if (userRole == "Clerk")
        {
            // Clerks only see their own sales
            query = query.Where(s => s.ApplicationUserId == userId);
        }
        else if (userRole == "Manager")
        {
            // Managers see sales from their quarries
            var managerQuarryIds = await context.Quarries
                .Where(q => q.ManagerId == userId)
                .Select(q => q.Id)
                .ToListAsync();

            query = query.Where(s => managerQuarryIds.Contains(s.QId));
        }
        // Administrators see all sales (no additional filter)

        // Apply quarry filter if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            query = query.Where(s => s.QId == quarryId);
        }

        // Apply date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(s => s.SaleDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.SaleDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync();

        var sales = await query
            .OrderByDescending(s => s.SaleDate)
            .ThenByDescending(s => s.DateCreated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SaleDto
            {
                Id = s.Id,
                SaleDate = s.SaleDate,
                VehicleRegistration = s.VehicleRegistration,
                ClientName = s.ClientName,
                ClientPhone = s.ClientPhone,
                Destination = s.Destination,
                ProductId = s.ProductId,
                ProductName = s.Product != null ? s.Product.ProductName : "",
                LayerId = s.LayerId,
                LayerLevel = s.Layer != null ? s.Layer.LayerLevel : "",
                Quantity = s.Quantity,
                PricePerUnit = s.PricePerUnit,
                GrossSaleAmount = s.GrossSaleAmount,
                BrokerId = s.BrokerId,
                BrokerName = s.Broker != null ? s.Broker.BrokerName : "",
                CommissionPerUnit = s.CommissionPerUnit,
                PaymentStatus = s.PaymentStatus,
                PaymentMode = s.PaymentMode,
                PaymentReference = s.PaymentReference,
                ClerkName = s.ClerkName,
                DateCreated = s.DateCreated
            })
            .ToListAsync();

        return Results.Ok(new
        {
            sales,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static async Task<IResult> GetSaleById(
        string id,
        ClaimsPrincipal user,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var sale = await context.Sales
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .Include(s => s.Clerk)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (sale == null)
        {
            return Results.NotFound(new { message = "Sale not found" });
        }

        // Check authorization
        if (userRole == "Clerk" && sale.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }
        else if (userRole == "Manager")
        {
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == sale.QId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }
        }

        var saleDto = new SaleDto
        {
            Id = sale.Id,
            SaleDate = sale.SaleDate,
            VehicleRegistration = sale.VehicleRegistration,
            ClientName = sale.ClientName,
            ClientPhone = sale.ClientPhone,
            Destination = sale.Destination,
            ProductId = sale.ProductId,
            ProductName = sale.Product?.ProductName ?? "",
            LayerId = sale.LayerId,
            LayerLevel = sale.Layer?.LayerLevel ?? "",
            Quantity = sale.Quantity,
            PricePerUnit = sale.PricePerUnit,
            GrossSaleAmount = sale.GrossSaleAmount,
            BrokerId = sale.BrokerId,
            BrokerName = sale.Broker?.BrokerName ?? "",
            CommissionPerUnit = sale.CommissionPerUnit,
            PaymentStatus = sale.PaymentStatus,
            PaymentMode = sale.PaymentMode,
            PaymentReference = sale.PaymentReference,
            ClerkName = sale.ClerkName,
            DateCreated = sale.DateCreated
        };

        return Results.Ok(saleDto);
    }

    private static async Task<IResult> CreateSale(
        [FromBody] CreateSaleRequest request,
        ClaimsPrincipal user,
        SaleService saleService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Only clerks can create sales
        if (userRole != "Clerk")
        {
            return Results.BadRequest(new { message = "Only clerks can record sales" });
        }

        // Get user's quarry ID
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
        {
            return Results.BadRequest(new { message = "User not assigned to a quarry" });
        }

        // Build Sale object
        var sale = new Sale
        {
            SaleDate = request.SaleDate,
            VehicleRegistration = request.VehicleRegistration,
            ProductId = request.ProductId,
            LayerId = request.LayerId,
            Quantity = request.Quantity,
            PricePerUnit = request.PricePerUnit,
            BrokerId = request.BrokerId,
            CommissionPerUnit = request.CommissionPerUnit,
            PaymentStatus = request.PaymentStatus,
            PaymentMode = request.PaymentMode,
            PaymentReference = request.PaymentReference,
            ClientName = request.ClientName,
            ClientPhone = request.ClientPhone,
            Destination = request.Destination
        };

        var result = await saleService.CreateSaleAsync(sale, userId, userEntity.QuarryId, userEntity.FullName);

        if (result.Success)
        {
            return Results.Created($"/api/sales/{result.Sale?.Id}", result.Sale);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateSale(
        string id,
        [FromBody] UpdateSaleRequest request,
        ClaimsPrincipal user,
        SaleService saleService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check if sale exists and user has access
        var existingSale = await context.Sales.FindAsync(id);
        if (existingSale == null || !existingSale.IsActive)
        {
            return Results.NotFound(new { message = "Sale not found" });
        }

        // Only the clerk who created the sale can update it
        if (existingSale.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        // Build updated Sale object
        var sale = new Sale
        {
            Id = id,
            SaleDate = request.SaleDate,
            VehicleRegistration = request.VehicleRegistration,
            ProductId = existingSale.ProductId,  // Cannot change product
            LayerId = existingSale.LayerId,      // Cannot change layer
            Quantity = request.Quantity,
            PricePerUnit = request.PricePerUnit,
            BrokerId = request.BrokerId,
            CommissionPerUnit = request.CommissionPerUnit,
            PaymentStatus = request.PaymentStatus,
            PaymentMode = request.PaymentMode,
            PaymentReference = request.PaymentReference,
            ClientName = request.ClientName,
            ClientPhone = request.ClientPhone,
            Destination = request.Destination
        };

        var result = await saleService.UpdateSaleAsync(sale, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteSale(
        string id,
        ClaimsPrincipal user,
        SaleService saleService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var sale = await context.Sales.FindAsync(id);
        if (sale == null || !sale.IsActive)
        {
            return Results.NotFound(new { message = "Sale not found" });
        }

        // Only the clerk who created the sale can delete it
        if (sale.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        var result = await saleService.DeleteSaleAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    // Commented out until GetDailySummaryAsync is implemented in SaleService
    // private static async Task<IResult> GetDailySummary(
    //     [FromQuery] DateTime fromDate,
    //     [FromQuery] DateTime toDate,
    //     [FromQuery] string? quarryId,
    //     ClaimsPrincipal user,
    //     SaleService saleService)
    // {
    //     var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //
    //     if (string.IsNullOrEmpty(userId))
    //     {
    //         return Results.Unauthorized();
    //     }
    //
    //     var summary = await saleService.GetDailySummaryAsync(fromDate, toDate, quarryId, userId);
    //
    //     return Results.Ok(summary);
    // }

    private static async Task<IResult> GetSalesByProduct(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? quarryId,
        ClaimsPrincipal user,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var query = context.Sales
            .Include(s => s.Product)
            .Where(s => s.IsActive)
            .Where(s => s.SaleDate >= fromDate && s.SaleDate <= toDate);

        // Apply role-based filtering
        if (userRole == "Clerk")
        {
            query = query.Where(s => s.ApplicationUserId == userId);
        }
        else if (userRole == "Manager")
        {
            var managerQuarryIds = await context.Quarries
                .Where(q => q.ManagerId == userId)
                .Select(q => q.Id)
                .ToListAsync();

            query = query.Where(s => managerQuarryIds.Contains(s.QId));
        }

        if (!string.IsNullOrEmpty(quarryId))
        {
            query = query.Where(s => s.QId == quarryId);
        }

        var productSummary = await query
            .GroupBy(s => new { s.ProductId, ProductName = s.Product!.ProductName })
            .Select(g => new ProductSalesDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                TotalQuantity = g.Sum(s => s.Quantity),
                TotalSales = g.Sum(s => s.GrossSaleAmount),
                SalesCount = g.Count()
            })
            .OrderByDescending(p => p.TotalSales)
            .ToListAsync();

        return Results.Ok(productSummary);
    }
}

public record SaleDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime? SaleDate { get; init; }
    public string VehicleRegistration { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? ClientPhone { get; init; }
    public string? Destination { get; init; }
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string LayerId { get; init; } = string.Empty;
    public string LayerLevel { get; init; } = string.Empty;
    public double Quantity { get; init; }
    public double PricePerUnit { get; init; }
    public double GrossSaleAmount { get; init; }
    public string? BrokerId { get; init; }
    public string? BrokerName { get; init; }
    public double CommissionPerUnit { get; init; }
    public string PaymentStatus { get; init; } = string.Empty;
    public string PaymentMode { get; init; } = string.Empty;
    public string? PaymentReference { get; init; }
    public string ClerkName { get; init; } = string.Empty;
    public DateTime DateCreated { get; init; }
}

public record CreateSaleRequest(
    DateTime SaleDate,
    string VehicleRegistration,
    string ProductId,
    string LayerId,
    double Quantity,
    double PricePerUnit,
    string? BrokerId,
    double CommissionPerUnit,
    string PaymentStatus,
    string PaymentMode,
    string? PaymentReference,
    string? ClientName,
    string? ClientPhone,
    string? Destination
);

public record UpdateSaleRequest(
    DateTime SaleDate,
    string VehicleRegistration,
    double Quantity,
    double PricePerUnit,
    string? BrokerId,
    double CommissionPerUnit,
    string PaymentStatus,
    string PaymentMode,
    string? PaymentReference,
    string? ClientName,
    string? ClientPhone,
    string? Destination
);

public record ProductSalesDto
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public double TotalQuantity { get; init; }
    public double TotalSales { get; init; }
    public int SalesCount { get; init; }
}
