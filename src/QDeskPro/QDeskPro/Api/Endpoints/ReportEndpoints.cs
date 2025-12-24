using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Features.Reports.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class ReportEndpoints
{
    public static void MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        group.MapGet("sales", GenerateSalesReport)
            .WithName("GenerateSalesReport")
            .WithDescription("Generate sales report (returns report data)");

        group.MapGet("sales/excel", GenerateSalesReportExcel)
            .WithName("GenerateSalesReportExcel")
            .WithDescription("Generate sales report as Excel download");

        group.MapGet("cashflow/excel", GenerateCashFlowReportExcel)
            .WithName("GenerateCashFlowReportExcel")
            .WithDescription("Generate cash flow report as Excel download")
            .RequireAuthorization(policy => policy.RequireRole("Administrator", "Manager"));

        group.MapGet("clerk/pdf", GenerateClerkReportPdf)
            .WithName("GenerateClerkReportPdf")
            .WithDescription("Generate clerk sales report as PDF download")
            .AllowAnonymous();

        group.MapGet("clerk/excel", GenerateClerkReportExcel)
            .WithName("GenerateClerkReportExcel")
            .WithDescription("Generate clerk sales report as Excel download")
            .AllowAnonymous();
    }

    private static async Task<IResult> GenerateSalesReport(
        ClaimsPrincipal user,
        AppDbContext context,
        ReportService reportService,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? quarryId = null)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Determine which quarry to report on
        string targetQuarryId;

        if (userRole == "Clerk")
        {
            // Clerks can only generate reports for their own quarry
            var userEntity = await context.Users.FindAsync(userId);
            if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
            {
                return Results.BadRequest(new { message = "User not assigned to a quarry" });
            }
            targetQuarryId = userEntity.QuarryId;

            // Generate clerk-specific report
            var clerkReport = await reportService.GenerateClerkReportAsync(fromDate, toDate, targetQuarryId, userId);
            return Results.Ok(clerkReport);
        }
        else if (userRole == "Manager")
        {
            // Managers must specify quarry ID
            if (string.IsNullOrEmpty(quarryId))
            {
                return Results.BadRequest(new { message = "Quarry ID is required for managers" });
            }

            // Verify manager owns this quarry
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == quarryId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }

            targetQuarryId = quarryId;

            // Generate manager report (all clerks in the quarry)
            var managerReport = await reportService.GenerateReportAsync(targetQuarryId, fromDate, toDate);
            return Results.Ok(managerReport);
        }
        else if (userRole == "Administrator")
        {
            // Admins must specify quarry ID
            if (string.IsNullOrEmpty(quarryId))
            {
                return Results.BadRequest(new { message = "Quarry ID is required for administrators" });
            }

            targetQuarryId = quarryId;

            // Generate admin report (all clerks in the quarry)
            var adminReport = await reportService.GenerateReportAsync(targetQuarryId, fromDate, toDate);
            return Results.Ok(adminReport);
        }

        return Results.BadRequest(new { message = "Invalid user role" });
    }

    private static async Task<IResult> GenerateSalesReportExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        ExcelExportService excelService,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? quarryId = null,
        [FromQuery] string? clerkUserId = null)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Determine which quarry to report on
        string targetQuarryId;
        string? targetClerkUserId = null;

        if (userRole == "Clerk")
        {
            // Clerks can only generate reports for their own quarry and their own data
            var userEntity = await context.Users.FindAsync(userId);
            if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
            {
                return Results.BadRequest(new { message = "User not assigned to a quarry" });
            }
            targetQuarryId = userEntity.QuarryId;
            targetClerkUserId = userId; // Filter by this clerk
        }
        else if (userRole == "Manager")
        {
            // Managers must specify quarry ID
            if (string.IsNullOrEmpty(quarryId))
            {
                return Results.BadRequest(new { message = "Quarry ID is required for managers" });
            }

            // Verify manager owns this quarry
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == quarryId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }

            targetQuarryId = quarryId;
            targetClerkUserId = clerkUserId; // Optional: filter by specific clerk
        }
        else if (userRole == "Administrator")
        {
            // Admins must specify quarry ID
            if (string.IsNullOrEmpty(quarryId))
            {
                return Results.BadRequest(new { message = "Quarry ID is required for administrators" });
            }

            targetQuarryId = quarryId;
            targetClerkUserId = clerkUserId; // Optional: filter by specific clerk
        }
        else
        {
            return Results.BadRequest(new { message = "Invalid user role" });
        }

        // Generate Excel report
        var excelBytes = await excelService.GenerateSalesReportAsync(
            targetQuarryId,
            fromDate,
            toDate,
            targetClerkUserId
        );

        // Get quarry name for filename
        var quarry = await context.Quarries.FindAsync(targetQuarryId);
        var quarryName = quarry?.QuarryName ?? "Unknown";
        var fileName = $"Sales_Report_{quarryName}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> GenerateCashFlowReportExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        ExcelExportService excelService,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? quarryId = null)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Only managers and admins can generate cash flow reports
        if (userRole != "Administrator" && userRole != "Manager")
        {
            return Results.Forbid();
        }

        // Quarry ID is required
        if (string.IsNullOrEmpty(quarryId))
        {
            return Results.BadRequest(new { message = "Quarry ID is required" });
        }

        // Verify manager owns this quarry (admins skip this check)
        if (userRole == "Manager")
        {
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == quarryId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }
        }

        // Generate Excel report
        var excelBytes = await excelService.GenerateCashFlowReportAsync(
            quarryId,
            fromDate,
            toDate
        );

        // Get quarry name for filename
        var quarry = await context.Quarries.FindAsync(quarryId);
        var quarryName = quarry?.QuarryName ?? "Unknown";
        var fileName = $"CashFlow_Report_{quarryName}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> GenerateClerkReportPdf(
        AppDbContext context,
        ReportService reportService,
        ReportExportService exportService,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? quarryId = null,
        [FromQuery] string? userId = null)
    {
        // If no parameters provided, return bad request
        if (string.IsNullOrEmpty(quarryId) || string.IsNullOrEmpty(userId))
        {
            return Results.BadRequest(new { message = "Quarry ID and User ID are required" });
        }

        // Validate that the quarry and user exist
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null)
        {
            return Results.BadRequest(new { message = "Invalid user" });
        }

        var quarry = await context.Quarries.FindAsync(quarryId);
        if (quarry == null)
        {
            return Results.BadRequest(new { message = "Invalid quarry" });
        }

        // Generate report data
        var reportData = await reportService.GenerateClerkReportAsync(fromDate, toDate, quarryId, userId);

        // Get daily notes (for single day reports)
        string? dailyNotes = null;
        if (reportData.IsSingleDay)
        {
            var dateStamp = fromDate.ToString("yyyyMMdd");
            var note = await context.DailyNotes
                .Where(n => n.DateStamp == dateStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            dailyNotes = note?.Notes;
        }

        // Generate PDF
        var pdfBytes = exportService.GeneratePdfReport(reportData, dailyNotes);

        // Create filename
        var fileName = $"Sales_Report_{reportData.QuarryName}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.pdf";

        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    private static async Task<IResult> GenerateClerkReportExcel(
        AppDbContext context,
        ReportService reportService,
        ReportExportService exportService,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] string? quarryId = null,
        [FromQuery] string? userId = null)
    {
        // If no parameters provided, return bad request
        if (string.IsNullOrEmpty(quarryId) || string.IsNullOrEmpty(userId))
        {
            return Results.BadRequest(new { message = "Quarry ID and User ID are required" });
        }

        // Validate that the quarry and user exist
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null)
        {
            return Results.BadRequest(new { message = "Invalid user" });
        }

        var quarry = await context.Quarries.FindAsync(quarryId);
        if (quarry == null)
        {
            return Results.BadRequest(new { message = "Invalid quarry" });
        }

        // Generate report data
        var reportData = await reportService.GenerateClerkReportAsync(fromDate, toDate, quarryId, userId);

        // Get daily notes (for single day reports)
        string? dailyNotes = null;
        if (reportData.IsSingleDay)
        {
            var dateStamp = fromDate.ToString("yyyyMMdd");
            var note = await context.DailyNotes
                .Where(n => n.DateStamp == dateStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            dailyNotes = note?.Notes;
        }

        // Generate Excel
        var excelBytes = exportService.GenerateExcelReport(reportData, dailyNotes);

        // Create filename
        var fileName = $"Sales_Report_{reportData.QuarryName}_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.xlsx";

        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
