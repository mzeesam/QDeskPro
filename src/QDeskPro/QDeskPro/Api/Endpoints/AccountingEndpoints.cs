using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Accounting.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class AccountingEndpoints
{
    public static void MapAccountingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting")
            .WithTags("Accounting")
            .RequireAuthorization(policy => policy.RequireRole("Administrator", "Manager"))
            .RequireRateLimiting("api");

        // ===== Chart of Accounts =====
        group.MapGet("chart-of-accounts", GetChartOfAccounts)
            .WithName("GetChartOfAccounts")
            .WithDescription("Get chart of accounts for a quarry");

        group.MapGet("accounts/{id}", GetAccountById)
            .WithName("GetAccountById")
            .WithDescription("Get a specific ledger account by ID");

        group.MapPost("accounts", CreateAccount)
            .WithName("CreateAccount")
            .WithDescription("Create a new ledger account");

        group.MapPut("accounts/{id}", UpdateAccount)
            .WithName("UpdateAccount")
            .WithDescription("Update an existing ledger account");

        group.MapDelete("accounts/{id}", DeleteAccount)
            .WithName("DeleteAccount")
            .WithDescription("Delete a ledger account");

        // ===== Journal Entries =====
        group.MapGet("journal-entries", GetJournalEntries)
            .WithName("GetJournalEntries")
            .WithDescription("Get journal entries for a date range");

        group.MapGet("journal-entries/{id}", GetJournalEntryById)
            .WithName("GetJournalEntryById")
            .WithDescription("Get a specific journal entry by ID");

        group.MapPost("journal-entries", CreateJournalEntry)
            .WithName("CreateJournalEntry")
            .WithDescription("Create a manual journal entry");

        group.MapPost("journal-entries/{id}/post", PostJournalEntry)
            .WithName("PostJournalEntry")
            .WithDescription("Post a journal entry");

        group.MapPost("journal-entries/{id}/unpost", UnpostJournalEntry)
            .WithName("UnpostJournalEntry")
            .WithDescription("Unpost a journal entry");

        group.MapDelete("journal-entries/{id}", DeleteJournalEntry)
            .WithName("DeleteJournalEntry")
            .WithDescription("Delete a journal entry");

        // ===== Journal Entry Generation =====
        group.MapPost("regenerate-entries", RegenerateJournalEntries)
            .WithName("RegenerateJournalEntries")
            .WithDescription("Regenerate journal entries from transactions for a date range");

        // ===== Financial Reports (JSON) =====
        group.MapGet("trial-balance", GetTrialBalance)
            .WithName("GetTrialBalance")
            .WithDescription("Generate trial balance report");

        group.MapGet("profit-loss", GetProfitLoss)
            .WithName("GetProfitLoss")
            .WithDescription("Generate profit and loss statement");

        group.MapGet("balance-sheet", GetBalanceSheet)
            .WithName("GetBalanceSheet")
            .WithDescription("Generate balance sheet");

        group.MapGet("cash-flow", GetCashFlow)
            .WithName("GetCashFlow")
            .WithDescription("Generate cash flow statement");

        group.MapGet("ar-aging", GetARAgingReport)
            .WithName("GetARAgingReport")
            .WithDescription("Generate accounts receivable aging report");

        group.MapGet("ap-summary", GetAPSummaryReport)
            .WithName("GetAPSummaryReport")
            .WithDescription("Generate accounts payable summary report");

        group.MapGet("general-ledger/{accountId}", GetGeneralLedger)
            .WithName("GetGeneralLedger")
            .WithDescription("Generate general ledger report for a specific account");

        // ===== PDF Exports =====
        group.MapGet("reports/trial-balance/pdf", ExportTrialBalancePdf)
            .WithName("ExportTrialBalancePdf")
            .WithDescription("Export trial balance as PDF");

        group.MapGet("reports/profit-loss/pdf", ExportProfitLossPdf)
            .WithName("ExportProfitLossPdf")
            .WithDescription("Export profit and loss statement as PDF");

        group.MapGet("reports/balance-sheet/pdf", ExportBalanceSheetPdf)
            .WithName("ExportBalanceSheetPdf")
            .WithDescription("Export balance sheet as PDF");

        group.MapGet("reports/cash-flow/pdf", ExportCashFlowPdf)
            .WithName("ExportCashFlowPdf")
            .WithDescription("Export cash flow statement as PDF");

        group.MapGet("reports/ar-aging/pdf", ExportARAgingPdf)
            .WithName("ExportARAgingPdf")
            .WithDescription("Export AR aging report as PDF");

        group.MapGet("reports/ap-summary/pdf", ExportAPSummaryPdf)
            .WithName("ExportAPSummaryPdf")
            .WithDescription("Export AP summary report as PDF");

        // ===== Excel Exports =====
        group.MapGet("reports/trial-balance/excel", ExportTrialBalanceExcel)
            .WithName("ExportTrialBalanceExcel")
            .WithDescription("Export trial balance as Excel");

        group.MapGet("reports/profit-loss/excel", ExportProfitLossExcel)
            .WithName("ExportProfitLossExcel")
            .WithDescription("Export profit and loss statement as Excel");

        group.MapGet("reports/balance-sheet/excel", ExportBalanceSheetExcel)
            .WithName("ExportBalanceSheetExcel")
            .WithDescription("Export balance sheet as Excel");

        group.MapGet("reports/cash-flow/excel", ExportCashFlowExcel)
            .WithName("ExportCashFlowExcel")
            .WithDescription("Export cash flow statement as Excel");

        group.MapGet("reports/ar-aging/excel", ExportARAgingExcel)
            .WithName("ExportARAgingExcel")
            .WithDescription("Export AR aging report as Excel");

        group.MapGet("reports/ap-summary/excel", ExportAPSummaryExcel)
            .WithName("ExportAPSummaryExcel")
            .WithDescription("Export AP summary report as Excel");

        // ===== Combined Financial Package =====
        group.MapGet("reports/package/excel", ExportFinancialPackage)
            .WithName("ExportFinancialPackage")
            .WithDescription("Export all financial reports in one Excel workbook");

        // ===== Accounting Periods =====
        group.MapGet("periods", GetAccountingPeriods)
            .WithName("GetAccountingPeriods")
            .WithDescription("Get accounting periods for a quarry");

        group.MapPost("periods/{id}/close", ClosePeriod)
            .WithName("ClosePeriod")
            .WithDescription("Close an accounting period");

        group.MapPost("periods/{id}/reopen", ReopenPeriod)
            .WithName("ReopenPeriod")
            .WithDescription("Reopen a closed accounting period");

        // ===== Initialization =====
        group.MapPost("initialize", InitializeChartOfAccounts)
            .WithName("InitializeChartOfAccounts")
            .WithDescription("Initialize chart of accounts for a quarry");
    }

    #region Chart of Accounts

    private static async Task<IResult> GetChartOfAccounts(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromQuery] string quarryId)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var accounts = await accountingService.GetChartOfAccountsAsync(quarryId);
        return Results.Ok(accounts);
    }

    private static async Task<IResult> GetAccountById(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var account = await accountingService.GetAccountByIdAsync(id);
        if (account == null)
            return Results.NotFound(new { message = "Account not found" });

        if (!await ValidateQuarryAccess(user, context, account.QId!))
            return Results.Forbid();

        return Results.Ok(account);
    }

    private static async Task<IResult> CreateAccount(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromBody] LedgerAccount account)
    {
        if (string.IsNullOrEmpty(account.QId))
            return Results.BadRequest(new { message = "QuarryId is required" });

        if (!await ValidateQuarryAccess(user, context, account.QId))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        account.CreatedBy = userId;

        try
        {
            var created = await accountingService.CreateAccountAsync(account);
            return Results.Created($"/api/accounting/accounts/{created.Id}", created);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateAccount(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id,
        [FromBody] LedgerAccount account)
    {
        var existing = await accountingService.GetAccountByIdAsync(id);
        if (existing == null)
            return Results.NotFound(new { message = "Account not found" });

        if (!await ValidateQuarryAccess(user, context, existing.QId!))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        account.Id = id;
        account.ModifiedBy = userId;

        try
        {
            var updated = await accountingService.UpdateAccountAsync(account);
            return Results.Ok(updated);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DeleteAccount(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var existing = await accountingService.GetAccountByIdAsync(id);
        if (existing == null)
            return Results.NotFound(new { message = "Account not found" });

        if (!await ValidateQuarryAccess(user, context, existing.QId!))
            return Results.Forbid();

        try
        {
            await accountingService.DeleteAccountAsync(id);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Journal Entries

    private static async Task<IResult> GetJournalEntries(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var entries = await accountingService.GetJournalEntriesAsync(quarryId, from, to);
        return Results.Ok(entries);
    }

    private static async Task<IResult> GetJournalEntryById(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var entry = await accountingService.GetJournalEntryByIdAsync(id);
        if (entry == null)
            return Results.NotFound(new { message = "Journal entry not found" });

        if (!await ValidateQuarryAccess(user, context, entry.QId!))
            return Results.Forbid();

        return Results.Ok(entry);
    }

    private static async Task<IResult> CreateJournalEntry(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromBody] JournalEntry entry)
    {
        if (string.IsNullOrEmpty(entry.QId))
            return Results.BadRequest(new { message = "QuarryId is required" });

        if (!await ValidateQuarryAccess(user, context, entry.QId))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var created = await accountingService.CreateManualJournalEntryAsync(entry, userId);
            return Results.Created($"/api/accounting/journal-entries/{created.Id}", created);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> PostJournalEntry(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var entry = await accountingService.GetJournalEntryByIdAsync(id);
        if (entry == null)
            return Results.NotFound(new { message = "Journal entry not found" });

        if (!await ValidateQuarryAccess(user, context, entry.QId!))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await accountingService.PostJournalEntryAsync(id, userId);
            return Results.Ok(new { message = "Journal entry posted successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UnpostJournalEntry(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var entry = await accountingService.GetJournalEntryByIdAsync(id);
        if (entry == null)
            return Results.NotFound(new { message = "Journal entry not found" });

        if (!await ValidateQuarryAccess(user, context, entry.QId!))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await accountingService.UnpostJournalEntryAsync(id, userId);
            return Results.Ok(new { message = "Journal entry unposted successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DeleteJournalEntry(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var entry = await accountingService.GetJournalEntryByIdAsync(id);
        if (entry == null)
            return Results.NotFound(new { message = "Journal entry not found" });

        if (!await ValidateQuarryAccess(user, context, entry.QId!))
            return Results.Forbid();

        try
        {
            await accountingService.DeleteJournalEntryAsync(id);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> RegenerateJournalEntries(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        await accountingService.RegenerateAllJournalEntriesAsync(quarryId, from, to);
        return Results.Ok(new { message = "Journal entries regenerated successfully" });
    }

    #endregion

    #region Financial Reports (JSON)

    private static async Task<IResult> GetTrialBalance(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateTrialBalanceAsync(quarryId, asOfDate);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetProfitLoss(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateProfitLossAsync(quarryId, from, to);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetBalanceSheet(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateBalanceSheetAsync(quarryId, asOfDate);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetCashFlow(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateCashFlowAsync(quarryId, from, to);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetARAgingReport(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateARAgingAsync(quarryId, asOfDate);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetAPSummaryReport(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateAPSummaryAsync(quarryId, asOfDate);
        return Results.Ok(report);
    }

    private static async Task<IResult> GetGeneralLedger(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        string accountId,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        try
        {
            var report = await reportService.GenerateGeneralLedgerAsync(quarryId, accountId, from, to);
            return Results.Ok(report);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region PDF Exports

    private static async Task<IResult> ExportTrialBalancePdf(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateTrialBalanceAsync(quarryId, asOfDate);
        var pdfBytes = exportService.ExportTrialBalanceToPdf(report);

        var fileName = $"Trial_Balance_{report.QuarryName}_{asOfDate:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    private static async Task<IResult> ExportProfitLossPdf(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateProfitLossAsync(quarryId, from, to);
        var pdfBytes = exportService.ExportProfitLossToPdf(report);

        var fileName = $"Profit_Loss_{report.QuarryName}_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    private static async Task<IResult> ExportBalanceSheetPdf(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateBalanceSheetAsync(quarryId, asOfDate);
        var pdfBytes = exportService.ExportBalanceSheetToPdf(report);

        var fileName = $"Balance_Sheet_{report.QuarryName}_{asOfDate:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    private static async Task<IResult> ExportCashFlowPdf(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateCashFlowAsync(quarryId, from, to);
        var pdfBytes = exportService.ExportCashFlowToPdf(report);

        var fileName = $"Cash_Flow_{report.QuarryName}_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    private static async Task<IResult> ExportARAgingPdf(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateARAgingAsync(quarryId, asOfDate);
        var pdfBytes = exportService.ExportARAgingToPdf(report);

        var fileName = $"AR_Aging_{report.QuarryName}_{asOfDate:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    private static async Task<IResult> ExportAPSummaryPdf(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateAPSummaryAsync(quarryId, asOfDate);
        var pdfBytes = exportService.ExportAPSummaryToPdf(report);

        var fileName = $"AP_Summary_{report.QuarryName}_{asOfDate:yyyyMMdd}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
    }

    #endregion

    #region Excel Exports

    private static async Task<IResult> ExportTrialBalanceExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateTrialBalanceAsync(quarryId, asOfDate);
        var excelBytes = exportService.ExportTrialBalanceToExcel(report);

        var fileName = $"Trial_Balance_{report.QuarryName}_{asOfDate:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> ExportProfitLossExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateProfitLossAsync(quarryId, from, to);
        var excelBytes = exportService.ExportProfitLossToExcel(report);

        var fileName = $"Profit_Loss_{report.QuarryName}_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> ExportBalanceSheetExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateBalanceSheetAsync(quarryId, asOfDate);
        var excelBytes = exportService.ExportBalanceSheetToExcel(report);

        var fileName = $"Balance_Sheet_{report.QuarryName}_{asOfDate:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> ExportCashFlowExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateCashFlowAsync(quarryId, from, to);
        var excelBytes = exportService.ExportCashFlowToExcel(report);

        var fileName = $"Cash_Flow_{report.QuarryName}_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> ExportARAgingExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateARAgingAsync(quarryId, asOfDate);
        var excelBytes = exportService.ExportARAgingToExcel(report);

        var fileName = $"AR_Aging_{report.QuarryName}_{asOfDate:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> ExportAPSummaryExcel(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime asOfDate)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var report = await reportService.GenerateAPSummaryAsync(quarryId, asOfDate);
        var excelBytes = exportService.ExportAPSummaryToExcel(report);

        var fileName = $"AP_Summary_{report.QuarryName}_{asOfDate:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static async Task<IResult> ExportFinancialPackage(
        ClaimsPrincipal user,
        AppDbContext context,
        IFinancialReportService reportService,
        IFinancialReportExportService exportService,
        [FromQuery] string quarryId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        // Generate all reports
        var trialBalance = await reportService.GenerateTrialBalanceAsync(quarryId, to);
        var profitLoss = await reportService.GenerateProfitLossAsync(quarryId, from, to);
        var balanceSheet = await reportService.GenerateBalanceSheetAsync(quarryId, to);
        var cashFlow = await reportService.GenerateCashFlowAsync(quarryId, from, to);
        var arAging = await reportService.GenerateARAgingAsync(quarryId, to);
        var apSummary = await reportService.GenerateAPSummaryAsync(quarryId, to);

        // Export to combined workbook
        var excelBytes = exportService.ExportFinancialPackageToExcel(
            trialBalance, profitLoss, balanceSheet, cashFlow, arAging, apSummary);

        var fileName = $"Financial_Package_{trialBalance.QuarryName}_{from:yyyyMMdd}_{to:yyyyMMdd}.xlsx";
        return Results.File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    #endregion

    #region Accounting Periods

    private static async Task<IResult> GetAccountingPeriods(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromQuery] string quarryId,
        [FromQuery] int? fiscalYear = null)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        var periods = await accountingService.GetAccountingPeriodsAsync(quarryId, fiscalYear);
        return Results.Ok(periods);
    }

    private static async Task<IResult> ClosePeriod(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id,
        [FromBody] ClosePeriodRequest? request = null)
    {
        var period = (await context.AccountingPeriods.FindAsync(id));
        if (period == null)
            return Results.NotFound(new { message = "Period not found" });

        if (!await ValidateQuarryAccess(user, context, period.QId!))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await accountingService.ClosePeriodAsync(id, userId, request?.ClosingNotes);
            return Results.Ok(new { message = "Period closed successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> ReopenPeriod(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        string id)
    {
        var period = (await context.AccountingPeriods.FindAsync(id));
        if (period == null)
            return Results.NotFound(new { message = "Period not found" });

        if (!await ValidateQuarryAccess(user, context, period.QId!))
            return Results.Forbid();

        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            await accountingService.ReopenPeriodAsync(id, userId);
            return Results.Ok(new { message = "Period reopened successfully" });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Initialization

    private static async Task<IResult> InitializeChartOfAccounts(
        ClaimsPrincipal user,
        AppDbContext context,
        IAccountingService accountingService,
        [FromQuery] string quarryId)
    {
        if (!await ValidateQuarryAccess(user, context, quarryId))
            return Results.Forbid();

        // Check if chart of accounts already exists
        var existingAccounts = await accountingService.GetChartOfAccountsAsync(quarryId);
        if (existingAccounts.Any())
            return Results.BadRequest(new { message = "Chart of accounts already initialized for this quarry" });

        await accountingService.InitializeChartOfAccountsAsync(quarryId);
        return Results.Ok(new { message = "Chart of accounts initialized successfully" });
    }

    #endregion

    #region Helper Methods

    private static async Task<bool> ValidateQuarryAccess(ClaimsPrincipal user, AppDbContext context, string quarryId)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
            return false;

        // Administrators have access to all quarries
        if (userRole == "Administrator")
            return true;

        // Managers have access to quarries they own
        if (userRole == "Manager")
        {
            return await context.Quarries
                .AnyAsync(q => q.Id == quarryId && q.ManagerId == userId);
        }

        return false;
    }

    #endregion
}

public class ClosePeriodRequest
{
    public string? ClosingNotes { get; set; }
}
