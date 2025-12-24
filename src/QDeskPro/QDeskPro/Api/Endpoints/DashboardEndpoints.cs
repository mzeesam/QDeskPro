using Microsoft.AspNetCore.Mvc;
using QDeskPro.Data;
using QDeskPro.Features.Dashboard.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        group.MapGet("stats", GetDashboardStats)
            .WithName("GetDashboardStats")
            .WithDescription("Get dashboard statistics for today")
            .RequireAuthorization(policy => policy.RequireRole("Clerk"));

        // Commented out until implemented in DashboardService
        // group.MapGet("trends", GetSalesTrends)
        //     .WithName("GetSalesTrends")
        //     .WithDescription("Get sales trends data for charts");
    }

    private static async Task<IResult> GetDashboardStats(
        ClaimsPrincipal user,
        DashboardService dashboardService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Get user's quarry ID
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
        {
            return Results.BadRequest(new { message = "User not assigned to a quarry" });
        }

        var stats = await dashboardService.GetDashboardStatsAsync(userId, userEntity.QuarryId);

        return Results.Ok(stats);
    }

    // Commented out until implemented in DashboardService
    // private static async Task<IResult> GetSalesTrends(
    //     [FromQuery] DateTime fromDate,
    //     [FromQuery] DateTime toDate,
    //     [FromQuery] string? quarryId,
    //     ClaimsPrincipal user,
    //     DashboardService dashboardService)
    // {
    //     var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    //
    //     if (string.IsNullOrEmpty(userId))
    //     {
    //         return Results.Unauthorized();
    //     }
    //
    //     var trends = await dashboardService.GetSalesTrendsAsync(fromDate, toDate, quarryId, userId);
    //
    //     return Results.Ok(trends);
    // }
}
