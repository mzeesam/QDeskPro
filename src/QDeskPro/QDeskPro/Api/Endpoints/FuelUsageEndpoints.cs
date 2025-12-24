using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.FuelUsage.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class FuelUsageEndpoints
{
    public static void MapFuelUsageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/fuel-usage")
            .WithTags("FuelUsage")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        group.MapGet("", GetFuelUsages)
            .WithName("GetFuelUsages")
            .WithDescription("Get fuel usage records with pagination and filtering");

        group.MapGet("{id}", GetFuelUsageById)
            .WithName("GetFuelUsageById")
            .WithDescription("Get fuel usage record details by ID");

        group.MapPost("", CreateFuelUsage)
            .WithName("CreateFuelUsage")
            .WithDescription("Create a new fuel usage record");

        group.MapPut("{id}", UpdateFuelUsage)
            .WithName("UpdateFuelUsage")
            .WithDescription("Update an existing fuel usage record");

        group.MapDelete("{id}", DeleteFuelUsage)
            .WithName("DeleteFuelUsage")
            .WithDescription("Soft delete a fuel usage record");
    }

    private static async Task<IResult> GetFuelUsages(
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

        var query = context.FuelUsages
            .Where(f => f.IsActive);

        // Apply role-based filtering
        // Note: Fuel usage is shared across quarry (not filtered by clerk user)
        if (userRole == "Clerk")
        {
            // Clerks see fuel usage from their assigned quarry
            var userEntity = await context.Users.FindAsync(userId);
            if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
            {
                return Results.BadRequest(new { message = "User not assigned to a quarry" });
            }
            query = query.Where(f => f.QId == userEntity.QuarryId);
        }
        else if (userRole == "Manager")
        {
            // Managers see fuel usage from their quarries
            var managerQuarryIds = await context.Quarries
                .Where(q => q.ManagerId == userId)
                .Select(q => q.Id)
                .ToListAsync();

            query = query.Where(f => managerQuarryIds.Contains(f.QId));
        }
        // Administrators see all fuel usage records (no additional filter)

        // Apply quarry filter if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            query = query.Where(f => f.QId == quarryId);
        }

        // Apply date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(f => f.UsageDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(f => f.UsageDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync();

        var fuelUsages = await query
            .OrderByDescending(f => f.UsageDate)
            .ThenByDescending(f => f.DateCreated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(f => new FuelUsageDto
            {
                Id = f.Id,
                UsageDate = f.UsageDate,
                OldStock = f.OldStock,
                NewStock = f.NewStock,
                MachinesLoaded = f.MachinesLoaded,
                WheelLoadersLoaded = f.WheelLoadersLoaded,
                TotalStock = f.TotalStock,
                Used = f.Used,
                Balance = f.Balance,
                DateCreated = f.DateCreated
            })
            .ToListAsync();

        return Results.Ok(new
        {
            fuelUsages,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static async Task<IResult> GetFuelUsageById(
        string id,
        ClaimsPrincipal user,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var fuelUsage = await context.FuelUsages
            .FirstOrDefaultAsync(f => f.Id == id && f.IsActive);

        if (fuelUsage == null)
        {
            return Results.NotFound(new { message = "Fuel usage record not found" });
        }

        // Check authorization
        if (userRole == "Clerk")
        {
            var userEntity = await context.Users.FindAsync(userId);
            if (userEntity == null || userEntity.QuarryId != fuelUsage.QId)
            {
                return Results.Forbid();
            }
        }
        else if (userRole == "Manager")
        {
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == fuelUsage.QId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }
        }

        var fuelUsageDto = new FuelUsageDto
        {
            Id = fuelUsage.Id,
            UsageDate = fuelUsage.UsageDate,
            OldStock = fuelUsage.OldStock,
            NewStock = fuelUsage.NewStock,
            MachinesLoaded = fuelUsage.MachinesLoaded,
            WheelLoadersLoaded = fuelUsage.WheelLoadersLoaded,
            TotalStock = fuelUsage.TotalStock,
            Used = fuelUsage.Used,
            Balance = fuelUsage.Balance,
            DateCreated = fuelUsage.DateCreated
        };

        return Results.Ok(fuelUsageDto);
    }

    private static async Task<IResult> CreateFuelUsage(
        [FromBody] CreateFuelUsageRequest request,
        ClaimsPrincipal user,
        FuelUsageService fuelUsageService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Only clerks can create fuel usage records
        if (userRole != "Clerk")
        {
            return Results.BadRequest(new { message = "Only clerks can record fuel usage" });
        }

        // Get user's quarry ID
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
        {
            return Results.BadRequest(new { message = "User not assigned to a quarry" });
        }

        // Build FuelUsage object
        var fuelUsage = new FuelUsage
        {
            UsageDate = request.UsageDate,
            OldStock = request.OldStock,
            NewStock = request.NewStock,
            MachinesLoaded = request.MachinesLoaded,
            WheelLoadersLoaded = request.WheelLoadersLoaded
        };

        var result = await fuelUsageService.CreateFuelUsageAsync(fuelUsage, userId, userEntity.QuarryId);

        if (result.Success)
        {
            return Results.Created($"/api/fuel-usage/{result.FuelUsage?.Id}", result.FuelUsage);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateFuelUsage(
        string id,
        [FromBody] UpdateFuelUsageRequest request,
        ClaimsPrincipal user,
        FuelUsageService fuelUsageService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check if fuel usage record exists and user has access
        var existingFuelUsage = await context.FuelUsages.FindAsync(id);
        if (existingFuelUsage == null || !existingFuelUsage.IsActive)
        {
            return Results.NotFound(new { message = "Fuel usage record not found" });
        }

        // Only the clerk who created the fuel usage record can update it
        if (existingFuelUsage.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        // Build updated FuelUsage object
        var fuelUsage = new FuelUsage
        {
            Id = id,
            UsageDate = request.UsageDate,
            OldStock = request.OldStock,
            NewStock = request.NewStock,
            MachinesLoaded = request.MachinesLoaded,
            WheelLoadersLoaded = request.WheelLoadersLoaded
        };

        var result = await fuelUsageService.UpdateFuelUsageAsync(fuelUsage, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteFuelUsage(
        string id,
        ClaimsPrincipal user,
        FuelUsageService fuelUsageService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var fuelUsage = await context.FuelUsages.FindAsync(id);
        if (fuelUsage == null || !fuelUsage.IsActive)
        {
            return Results.NotFound(new { message = "Fuel usage record not found" });
        }

        // Only the clerk who created the fuel usage record can delete it
        if (fuelUsage.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        var result = await fuelUsageService.DeleteFuelUsageAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }
}

public record FuelUsageDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime? UsageDate { get; init; }
    public double OldStock { get; init; }
    public double NewStock { get; init; }
    public double MachinesLoaded { get; init; }
    public double WheelLoadersLoaded { get; init; }
    public double TotalStock { get; init; }
    public double Used { get; init; }
    public double Balance { get; init; }
    public DateTime DateCreated { get; init; }
}

public record CreateFuelUsageRequest(
    DateTime UsageDate,
    double OldStock,
    double NewStock,
    double MachinesLoaded,
    double WheelLoadersLoaded
);

public record UpdateFuelUsageRequest(
    DateTime UsageDate,
    double OldStock,
    double NewStock,
    double MachinesLoaded,
    double WheelLoadersLoaded
);
