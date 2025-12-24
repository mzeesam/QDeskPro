using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Banking.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class BankingEndpoints
{
    public static void MapBankingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/banking")
            .WithTags("Banking")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        group.MapGet("", GetBankings)
            .WithName("GetBankings")
            .WithDescription("Get banking records with pagination and filtering");

        group.MapGet("{id}", GetBankingById)
            .WithName("GetBankingById")
            .WithDescription("Get banking record details by ID");

        group.MapPost("", CreateBanking)
            .WithName("CreateBanking")
            .WithDescription("Create a new banking record");

        group.MapPut("{id}", UpdateBanking)
            .WithName("UpdateBanking")
            .WithDescription("Update an existing banking record");

        group.MapDelete("{id}", DeleteBanking)
            .WithName("DeleteBanking")
            .WithDescription("Soft delete a banking record");
    }

    private static async Task<IResult> GetBankings(
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

        var query = context.Bankings
            .Where(b => b.IsActive);

        // Apply role-based filtering
        if (userRole == "Clerk")
        {
            // Clerks only see their own banking records
            query = query.Where(b => b.ApplicationUserId == userId);
        }
        else if (userRole == "Manager")
        {
            // Managers see banking records from their quarries
            var managerQuarryIds = await context.Quarries
                .Where(q => q.ManagerId == userId)
                .Select(q => q.Id)
                .ToListAsync();

            query = query.Where(b => managerQuarryIds.Contains(b.QId));
        }
        // Administrators see all banking records (no additional filter)

        // Apply quarry filter if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            query = query.Where(b => b.QId == quarryId);
        }

        // Apply date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(b => b.BankingDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(b => b.BankingDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync();

        var bankings = await query
            .OrderByDescending(b => b.BankingDate)
            .ThenByDescending(b => b.DateCreated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BankingDto
            {
                Id = b.Id,
                BankingDate = b.BankingDate,
                Item = b.Item,
                AmountBanked = b.AmountBanked,
                BalanceBF = b.BalanceBF,
                TxnReference = b.TxnReference,
                RefCode = b.RefCode,
                DateCreated = b.DateCreated
            })
            .ToListAsync();

        return Results.Ok(new
        {
            bankings,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static async Task<IResult> GetBankingById(
        string id,
        ClaimsPrincipal user,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var banking = await context.Bankings
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);

        if (banking == null)
        {
            return Results.NotFound(new { message = "Banking record not found" });
        }

        // Check authorization
        if (userRole == "Clerk" && banking.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }
        else if (userRole == "Manager")
        {
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == banking.QId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }
        }

        var bankingDto = new BankingDto
        {
            Id = banking.Id,
            BankingDate = banking.BankingDate,
            Item = banking.Item,
            AmountBanked = banking.AmountBanked,
            BalanceBF = banking.BalanceBF,
            TxnReference = banking.TxnReference,
            RefCode = banking.RefCode,
            DateCreated = banking.DateCreated
        };

        return Results.Ok(bankingDto);
    }

    private static async Task<IResult> CreateBanking(
        [FromBody] CreateBankingRequest request,
        ClaimsPrincipal user,
        BankingService bankingService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Only clerks can create banking records
        if (userRole != "Clerk")
        {
            return Results.BadRequest(new { message = "Only clerks can record banking transactions" });
        }

        // Get user's quarry ID
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
        {
            return Results.BadRequest(new { message = "User not assigned to a quarry" });
        }

        // Build Banking object
        var banking = new Banking
        {
            BankingDate = request.BankingDate,
            AmountBanked = request.AmountBanked,
            BalanceBF = request.BalanceBF,
            TxnReference = request.TxnReference
        };

        var result = await bankingService.CreateBankingAsync(banking, userId, userEntity.QuarryId);

        if (result.Success)
        {
            return Results.Created($"/api/banking/{result.Banking?.Id}", result.Banking);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateBanking(
        string id,
        [FromBody] UpdateBankingRequest request,
        ClaimsPrincipal user,
        BankingService bankingService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check if banking record exists and user has access
        var existingBanking = await context.Bankings.FindAsync(id);
        if (existingBanking == null || !existingBanking.IsActive)
        {
            return Results.NotFound(new { message = "Banking record not found" });
        }

        // Only the clerk who created the banking record can update it
        if (existingBanking.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        // Build updated Banking object
        var banking = new Banking
        {
            Id = id,
            BankingDate = request.BankingDate,
            AmountBanked = request.AmountBanked,
            BalanceBF = request.BalanceBF,
            TxnReference = request.TxnReference
        };

        var result = await bankingService.UpdateBankingAsync(banking, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteBanking(
        string id,
        ClaimsPrincipal user,
        BankingService bankingService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var banking = await context.Bankings.FindAsync(id);
        if (banking == null || !banking.IsActive)
        {
            return Results.NotFound(new { message = "Banking record not found" });
        }

        // Only the clerk who created the banking record can delete it
        if (banking.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        var result = await bankingService.DeleteBankingAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }
}

public record BankingDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime? BankingDate { get; init; }
    public string Item { get; init; } = string.Empty;
    public double AmountBanked { get; init; }
    public double BalanceBF { get; init; }
    public string? TxnReference { get; init; }
    public string? RefCode { get; init; }
    public DateTime DateCreated { get; init; }
}

public record CreateBankingRequest(
    DateTime BankingDate,
    double AmountBanked,
    double BalanceBF,
    string TxnReference
);

public record UpdateBankingRequest(
    DateTime BankingDate,
    double AmountBanked,
    double BalanceBF,
    string TxnReference
);
