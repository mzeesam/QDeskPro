using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

namespace QDeskPro.Features.Reports.Services;

/// <summary>
/// Service for managing persistent report history.
/// Enables users to save, retrieve, and search previously generated reports.
/// </summary>
public interface IReportHistoryService
{
    /// <summary>
    /// Saves a generated report to the database.
    /// </summary>
    Task<GeneratedReport> SaveReportAsync(GeneratedReport report);

    /// <summary>
    /// Gets report history for a quarry, ordered by most recent first.
    /// </summary>
    Task<List<GeneratedReport>> GetReportHistoryAsync(string quarryId, int limit = 20);

    /// <summary>
    /// Gets a specific report by ID.
    /// </summary>
    Task<GeneratedReport?> GetReportByIdAsync(string reportId);

    /// <summary>
    /// Soft deletes a report from history.
    /// </summary>
    Task DeleteReportAsync(string reportId);

    /// <summary>
    /// Searches reports with optional filters.
    /// </summary>
    Task<List<GeneratedReport>> SearchReportsAsync(
        string quarryId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? reportType = null,
        int limit = 50);

    /// <summary>
    /// Gets report history for multiple quarries (for managers with multiple quarries).
    /// </summary>
    Task<List<GeneratedReport>> GetReportHistoryForQuarriesAsync(List<string> quarryIds, int limit = 20);
}

/// <summary>
/// Implementation of report history service using Entity Framework.
/// </summary>
public class ReportHistoryService : IReportHistoryService
{
    private readonly AppDbContext _context;

    public ReportHistoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GeneratedReport> SaveReportAsync(GeneratedReport report)
    {
        if (string.IsNullOrEmpty(report.Id))
        {
            report.Id = Guid.NewGuid().ToString();
        }

        report.DateCreated = DateTime.UtcNow;
        report.IsActive = true;

        _context.GeneratedReports.Add(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task<List<GeneratedReport>> GetReportHistoryAsync(string quarryId, int limit = 20)
    {
        return await _context.GeneratedReports
            .Where(r => r.QuarryId == quarryId && r.IsActive)
            .OrderByDescending(r => r.DateCreated)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<GeneratedReport?> GetReportByIdAsync(string reportId)
    {
        return await _context.GeneratedReports
            .Include(r => r.Quarry)
            .FirstOrDefaultAsync(r => r.Id == reportId && r.IsActive);
    }

    public async Task DeleteReportAsync(string reportId)
    {
        var report = await _context.GeneratedReports.FindAsync(reportId);
        if (report != null)
        {
            report.IsActive = false;
            report.DateModified = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<GeneratedReport>> SearchReportsAsync(
        string quarryId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? reportType = null,
        int limit = 50)
    {
        var query = _context.GeneratedReports
            .Where(r => r.QuarryId == quarryId && r.IsActive);

        if (fromDate.HasValue)
            query = query.Where(r => r.FromDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(r => r.ToDate <= toDate.Value);

        if (!string.IsNullOrEmpty(reportType))
            query = query.Where(r => r.ReportType == reportType);

        return await query
            .OrderByDescending(r => r.DateCreated)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<GeneratedReport>> GetReportHistoryForQuarriesAsync(List<string> quarryIds, int limit = 20)
    {
        return await _context.GeneratedReports
            .Where(r => quarryIds.Contains(r.QuarryId) && r.IsActive)
            .OrderByDescending(r => r.DateCreated)
            .Take(limit)
            .ToListAsync();
    }
}
