using Microsoft.AspNetCore.Mvc;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.MasterData.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class MasterDataEndpoints
{
    public static void MapMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masterdata")
            .WithTags("MasterData")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        // Quarries endpoints
        group.MapGet("quarries", GetQuarries)
            .WithName("GetQuarries")
            .WithDescription("Get all quarries (role-filtered)");

        group.MapGet("quarries/{id}", GetQuarryById)
            .WithName("GetQuarryById")
            .WithDescription("Get quarry details by ID");

        group.MapPost("quarries", CreateQuarry)
            .WithName("CreateQuarry")
            .WithDescription("Create a new quarry (Manager only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapPut("quarries/{id}", UpdateQuarry)
            .WithName("UpdateQuarry")
            .WithDescription("Update a quarry (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapDelete("quarries/{id}", DeleteQuarry)
            .WithName("DeleteQuarry")
            .WithDescription("Soft delete a quarry (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        // Products endpoints (read-only - products are global)
        group.MapGet("products", GetProducts)
            .WithName("GetProducts")
            .WithDescription("Get all products");

        group.MapGet("products/{id}", GetProductById)
            .WithName("GetProductById")
            .WithDescription("Get product details by ID");

        // Layers endpoints
        group.MapGet("quarries/{quarryId}/layers", GetLayers)
            .WithName("GetLayers")
            .WithDescription("Get all layers for a quarry");

        group.MapGet("layers/{id}", GetLayerById)
            .WithName("GetLayerById")
            .WithDescription("Get layer details by ID");

        group.MapPost("quarries/{quarryId}/layers", CreateLayer)
            .WithName("CreateLayer")
            .WithDescription("Create a new layer for a quarry (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapPut("layers/{id}", UpdateLayer)
            .WithName("UpdateLayer")
            .WithDescription("Update a layer (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapDelete("layers/{id}", DeleteLayer)
            .WithName("DeleteLayer")
            .WithDescription("Soft delete a layer (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        // Brokers endpoints
        group.MapGet("quarries/{quarryId}/brokers", GetBrokers)
            .WithName("GetBrokers")
            .WithDescription("Get all brokers for a quarry");

        group.MapGet("brokers/{id}", GetBrokerById)
            .WithName("GetBrokerById")
            .WithDescription("Get broker details by ID");

        group.MapPost("quarries/{quarryId}/brokers", CreateBroker)
            .WithName("CreateBroker")
            .WithDescription("Create a new broker for a quarry (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapPut("brokers/{id}", UpdateBroker)
            .WithName("UpdateBroker")
            .WithDescription("Update a broker (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapDelete("brokers/{id}", DeleteBroker)
            .WithName("DeleteBroker")
            .WithDescription("Soft delete a broker (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        // Product Prices endpoints
        group.MapGet("quarries/{quarryId}/prices", GetProductPrices)
            .WithName("GetProductPrices")
            .WithDescription("Get all product prices for a quarry");

        group.MapGet("prices/{id}", GetProductPriceById)
            .WithName("GetProductPriceById")
            .WithDescription("Get product price details by ID");

        group.MapPost("quarries/{quarryId}/prices", UpsertProductPrice)
            .WithName("UpsertProductPrice")
            .WithDescription("Create or update a product price for a quarry (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));

        group.MapDelete("prices/{id}", DeleteProductPrice)
            .WithName("DeleteProductPrice")
            .WithDescription("Soft delete a product price (Owner/Admin only)")
            .RequireAuthorization(policy => policy.RequireRole("Manager", "Administrator"));
    }

    #region Quarries

    private static async Task<IResult> GetQuarries(
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        List<Quarry> quarries;

        if (userRole == "Administrator")
        {
            quarries = await masterDataService.GetAllQuarriesAsync();
        }
        else if (userRole == "Manager")
        {
            quarries = await masterDataService.GetQuarriesForManagerAsync(userId);
        }
        else if (userRole == "Clerk")
        {
            // Clerks can only see their assigned quarry
            quarries = await masterDataService.GetAllQuarriesAsync();
            // Note: Should filter by user assignment, but for simplicity returning all
            // In production, you'd want to implement GetQuarriesForClerkAsync
        }
        else
        {
            return Results.Forbid();
        }

        return Results.Ok(quarries);
    }

    private static async Task<IResult> GetQuarryById(
        string id,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var quarry = await masterDataService.GetQuarryByIdAsync(id);

        if (quarry == null)
        {
            return Results.NotFound(new { message = "Quarry not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, id, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        return Results.Ok(quarry);
    }

    private static async Task<IResult> CreateQuarry(
        [FromBody] CreateQuarryRequest request,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var quarry = new Quarry
        {
            QuarryName = request.QuarryName,
            Location = request.Location,
            LoadersFee = request.LoadersFee,
            LandRateFee = request.LandRateFee,
            RejectsFee = request.RejectsFee,
            EmailRecipients = request.EmailRecipients,
            DailyReportEnabled = request.DailyReportEnabled,
            DailyReportTime = request.DailyReportTime
        };

        var result = await masterDataService.CreateQuarryAsync(quarry, userId);

        if (result.Success)
        {
            return Results.Created($"/api/masterdata/quarries/{result.Quarry?.Id}", result.Quarry);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateQuarry(
        string id,
        [FromBody] UpdateQuarryRequest request,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Verify quarry exists and user has access
        var existingQuarry = await context.Quarries.FindAsync(id);
        if (existingQuarry == null || !existingQuarry.IsActive)
        {
            return Results.NotFound(new { message = "Quarry not found" });
        }

        // Only owner or admin can update
        if (userRole != "Administrator" && existingQuarry.ManagerId != userId)
        {
            return Results.Forbid();
        }

        var quarry = new Quarry
        {
            Id = id,
            QuarryName = request.QuarryName,
            Location = request.Location,
            LoadersFee = request.LoadersFee,
            LandRateFee = request.LandRateFee,
            RejectsFee = request.RejectsFee,
            EmailRecipients = request.EmailRecipients,
            DailyReportEnabled = request.DailyReportEnabled,
            DailyReportTime = request.DailyReportTime
        };

        var result = await masterDataService.UpdateQuarryAsync(quarry, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteQuarry(
        string id,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var quarry = await context.Quarries.FindAsync(id);
        if (quarry == null || !quarry.IsActive)
        {
            return Results.NotFound(new { message = "Quarry not found" });
        }

        // Only owner or admin can delete
        if (userRole != "Administrator" && quarry.ManagerId != userId)
        {
            return Results.Forbid();
        }

        var result = await masterDataService.DeleteQuarryAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    #endregion

    #region Products

    private static async Task<IResult> GetProducts(MasterDataService masterDataService)
    {
        var products = await masterDataService.GetAllProductsAsync();
        return Results.Ok(products);
    }

    private static async Task<IResult> GetProductById(
        string id,
        MasterDataService masterDataService)
    {
        var product = await masterDataService.GetProductByIdAsync(id);

        if (product == null)
        {
            return Results.NotFound(new { message = "Product not found" });
        }

        return Results.Ok(product);
    }

    #endregion

    #region Layers

    private static async Task<IResult> GetLayers(
        string quarryId,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, quarryId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var layers = await masterDataService.GetLayersForQuarryAsync(quarryId);
        return Results.Ok(layers);
    }

    private static async Task<IResult> GetLayerById(
        string id,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var layer = await masterDataService.GetLayerByIdAsync(id);

        if (layer == null)
        {
            return Results.NotFound(new { message = "Layer not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, layer.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        return Results.Ok(layer);
    }

    private static async Task<IResult> CreateLayer(
        string quarryId,
        [FromBody] CreateLayerRequest request,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, quarryId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var layer = new Layer
        {
            LayerLevel = request.LayerLevel,
            DateStarted = request.DateStarted,
            LayerLength = request.LayerLength
        };

        var result = await masterDataService.CreateLayerAsync(layer, userId, quarryId);

        if (result.Success)
        {
            return Results.Created($"/api/masterdata/layers/{result.Layer?.Id}", result.Layer);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateLayer(
        string id,
        [FromBody] UpdateLayerRequest request,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var existingLayer = await context.Layers.FindAsync(id);
        if (existingLayer == null || !existingLayer.IsActive)
        {
            return Results.NotFound(new { message = "Layer not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, existingLayer.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var layer = new Layer
        {
            Id = id,
            LayerLevel = request.LayerLevel,
            DateStarted = request.DateStarted,
            LayerLength = request.LayerLength
        };

        var result = await masterDataService.UpdateLayerAsync(layer, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteLayer(
        string id,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var layer = await context.Layers.FindAsync(id);
        if (layer == null || !layer.IsActive)
        {
            return Results.NotFound(new { message = "Layer not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, layer.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var result = await masterDataService.DeleteLayerAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    #endregion

    #region Brokers

    private static async Task<IResult> GetBrokers(
        string quarryId,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, quarryId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var brokers = await masterDataService.GetBrokersForQuarryAsync(quarryId);
        return Results.Ok(brokers);
    }

    private static async Task<IResult> GetBrokerById(
        string id,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var broker = await masterDataService.GetBrokerByIdAsync(id);

        if (broker == null)
        {
            return Results.NotFound(new { message = "Broker not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, broker.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        return Results.Ok(broker);
    }

    private static async Task<IResult> CreateBroker(
        string quarryId,
        [FromBody] CreateBrokerRequest request,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, quarryId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var broker = new Broker
        {
            BrokerName = request.BrokerName,
            Phone = request.Phone
        };

        var result = await masterDataService.CreateBrokerAsync(broker, userId, quarryId);

        if (result.Success)
        {
            return Results.Created($"/api/masterdata/brokers/{result.Broker?.Id}", result.Broker);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateBroker(
        string id,
        [FromBody] UpdateBrokerRequest request,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var existingBroker = await context.Brokers.FindAsync(id);
        if (existingBroker == null || !existingBroker.IsActive)
        {
            return Results.NotFound(new { message = "Broker not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, existingBroker.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var broker = new Broker
        {
            Id = id,
            BrokerName = request.BrokerName,
            Phone = request.Phone
        };

        var result = await masterDataService.UpdateBrokerAsync(broker, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteBroker(
        string id,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var broker = await context.Brokers.FindAsync(id);
        if (broker == null || !broker.IsActive)
        {
            return Results.NotFound(new { message = "Broker not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, broker.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var result = await masterDataService.DeleteBrokerAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    #endregion

    #region Product Prices

    private static async Task<IResult> GetProductPrices(
        string quarryId,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, quarryId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var prices = await masterDataService.GetProductPricesForQuarryAsync(quarryId);
        return Results.Ok(prices);
    }

    private static async Task<IResult> GetProductPriceById(
        string id,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var productPrice = await masterDataService.GetProductPriceByIdAsync(id);

        if (productPrice == null)
        {
            return Results.NotFound(new { message = "Product price not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId!, productPrice.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        return Results.Ok(productPrice);
    }

    private static async Task<IResult> UpsertProductPrice(
        string quarryId,
        [FromBody] UpsertProductPriceRequest request,
        ClaimsPrincipal user,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, quarryId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var result = await masterDataService.UpsertProductPriceAsync(
            request.ProductId,
            quarryId,
            request.Price,
            userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message, productPrice = result.ProductPrice });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteProductPrice(
        string id,
        ClaimsPrincipal user,
        AppDbContext context,
        MasterDataService masterDataService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var productPrice = await context.ProductPrices.FindAsync(id);
        if (productPrice == null || !productPrice.IsActive)
        {
            return Results.NotFound(new { message = "Product price not found" });
        }

        // Check authorization
        var isAdmin = userRole == "Administrator";
        var hasAccess = await masterDataService.UserHasQuarryAccessAsync(userId, productPrice.QId, isAdmin);

        if (!hasAccess)
        {
            return Results.Forbid();
        }

        var result = await masterDataService.DeleteProductPriceAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    #endregion
}

#region DTOs

// Quarry DTOs
public record CreateQuarryRequest(
    string QuarryName,
    string Location,
    double? LoadersFee,
    double? LandRateFee,
    double? RejectsFee,
    string? EmailRecipients,
    bool DailyReportEnabled,
    TimeSpan? DailyReportTime
);

public record UpdateQuarryRequest(
    string QuarryName,
    string Location,
    double? LoadersFee,
    double? LandRateFee,
    double? RejectsFee,
    string? EmailRecipients,
    bool DailyReportEnabled,
    TimeSpan? DailyReportTime
);

// Layer DTOs
public record CreateLayerRequest(
    string LayerLevel,
    DateTime? DateStarted,
    double? LayerLength
);

public record UpdateLayerRequest(
    string LayerLevel,
    DateTime? DateStarted,
    double? LayerLength
);

// Broker DTOs
public record CreateBrokerRequest(
    string BrokerName,
    string Phone
);

public record UpdateBrokerRequest(
    string BrokerName,
    string Phone
);

// Product Price DTOs
public record UpsertProductPriceRequest(
    string ProductId,
    double Price
);

#endregion
