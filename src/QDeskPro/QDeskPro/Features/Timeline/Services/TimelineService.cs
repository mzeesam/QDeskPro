namespace QDeskPro.Features.Timeline.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for managing quarry comments, notes, reminders, and tasks
/// </summary>
public class TimelineService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TimelineService> _logger;

    public TimelineService(AppDbContext context, ILogger<TimelineService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Create a new comment/note/reminder/task
    /// </summary>
    public async Task<(bool Success, string Message, QuarryComment? Comment)> CreateCommentAsync(
        QuarryComment comment,
        string userId,
        string authorName,
        string quarryId)
    {
        try
        {
            // Validation
            var validationErrors = ValidateComment(comment);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            // Set audit fields
            comment.Id = Guid.NewGuid().ToString();
            comment.QuarryId = quarryId;
            comment.ApplicationUserId = userId;
            comment.AuthorName = authorName;
            comment.DateCreated = DateTime.UtcNow;
            comment.CreatedBy = userId;
            comment.IsActive = true;
            comment.DateStamp = DateTime.Today.ToString("yyyyMMdd");

            // Default values
            comment.CommentType ??= "Note";
            if (comment.CommentType != "Note" && string.IsNullOrEmpty(comment.Priority))
            {
                comment.Priority = "Medium";
            }

            _context.QuarryComments.Add(comment);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Comment created: {CommentId} | Type: {Type} | By: {Author} | Quarry: {QuarryId}",
                comment.Id, comment.CommentType, authorName, quarryId);

            return (true, $"{comment.CommentType} created successfully!", comment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create comment");
            return (false, $"Error creating comment: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get comments for a quarry with optional filters
    /// </summary>
    public async Task<List<QuarryComment>> GetCommentsAsync(
        string quarryId,
        string? commentType = null,
        bool? pendingOnly = null,
        string? linkedEntityType = null,
        string? linkedEntityId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? limit = null)
    {
        var query = _context.QuarryComments
            .Where(c => c.QuarryId == quarryId)
            .Where(c => c.IsActive)
            .AsQueryable();

        // Filter by comment type
        if (!string.IsNullOrEmpty(commentType))
        {
            query = query.Where(c => c.CommentType == commentType);
        }

        // Filter pending tasks/reminders only
        if (pendingOnly == true)
        {
            query = query.Where(c => c.CommentType != "Note" && !c.IsCompleted);
        }

        // Filter by linked entity
        if (!string.IsNullOrEmpty(linkedEntityType))
        {
            query = query.Where(c => c.LinkedEntityType == linkedEntityType);
        }

        if (!string.IsNullOrEmpty(linkedEntityId))
        {
            query = query.Where(c => c.LinkedEntityId == linkedEntityId);
        }

        // Date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(c => c.DateCreated >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var endDate = toDate.Value.Date.AddDays(1);
            query = query.Where(c => c.DateCreated < endDate);
        }

        // Order by most recent first
        query = query.OrderByDescending(c => c.DateCreated);

        // Apply limit if specified
        if (limit.HasValue && limit.Value > 0)
        {
            query = query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    /// <summary>
    /// Get comments grouped by date for timeline display
    /// </summary>
    public async Task<Dictionary<DateTime, List<QuarryComment>>> GetTimelineAsync(
        string quarryId,
        string? commentType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int days = 30)
    {
        // Default date range
        fromDate ??= DateTime.Today.AddDays(-days);
        toDate ??= DateTime.Today;

        var comments = await GetCommentsAsync(
            quarryId,
            commentType: commentType,
            fromDate: fromDate,
            toDate: toDate);

        // Group by date
        return comments
            .GroupBy(c => c.DateCreated.Date)
            .OrderByDescending(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Get pending tasks and reminders count for a quarry
    /// </summary>
    public async Task<(int Tasks, int Reminders, int Overdue)> GetPendingCountsAsync(string quarryId)
    {
        var pendingComments = await _context.QuarryComments
            .Where(c => c.QuarryId == quarryId)
            .Where(c => c.IsActive)
            .Where(c => !c.IsCompleted)
            .Where(c => c.CommentType == "Task" || c.CommentType == "Reminder")
            .ToListAsync();

        var tasks = pendingComments.Count(c => c.CommentType == "Task");
        var reminders = pendingComments.Count(c => c.CommentType == "Reminder");
        var overdue = pendingComments.Count(c => c.DueDate.HasValue && c.DueDate.Value.Date < DateTime.Today);

        return (tasks, reminders, overdue);
    }

    /// <summary>
    /// Get overdue reminders and tasks for a quarry
    /// </summary>
    public async Task<List<QuarryComment>> GetOverdueItemsAsync(string quarryId)
    {
        return await _context.QuarryComments
            .Where(c => c.QuarryId == quarryId)
            .Where(c => c.IsActive)
            .Where(c => !c.IsCompleted)
            .Where(c => c.DueDate.HasValue && c.DueDate.Value.Date < DateTime.Today)
            .OrderBy(c => c.DueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Get upcoming reminders and tasks (due in next N days)
    /// </summary>
    public async Task<List<QuarryComment>> GetUpcomingItemsAsync(string quarryId, int days = 7)
    {
        var endDate = DateTime.Today.AddDays(days);

        return await _context.QuarryComments
            .Where(c => c.QuarryId == quarryId)
            .Where(c => c.IsActive)
            .Where(c => !c.IsCompleted)
            .Where(c => c.DueDate.HasValue && c.DueDate.Value.Date <= endDate)
            .OrderBy(c => c.DueDate)
            .ToListAsync();
    }

    /// <summary>
    /// Mark a task or reminder as completed
    /// </summary>
    public async Task<(bool Success, string Message)> MarkAsCompletedAsync(string commentId, string userId)
    {
        try
        {
            var comment = await _context.QuarryComments.FindAsync(commentId);
            if (comment == null)
            {
                return (false, "Comment not found");
            }

            if (comment.CommentType == "Note")
            {
                return (false, "Notes cannot be marked as completed");
            }

            comment.IsCompleted = true;
            comment.CompletedDate = DateTime.UtcNow;
            comment.DateModified = DateTime.UtcNow;
            comment.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Comment completed: {CommentId} | Type: {Type} | By: {UserId}",
                commentId, comment.CommentType, userId);

            return (true, $"{comment.CommentType} marked as completed!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark comment as completed: {CommentId}", commentId);
            return (false, $"Error updating comment: {ex.Message}");
        }
    }

    /// <summary>
    /// Mark a task or reminder as not completed (reopen)
    /// </summary>
    public async Task<(bool Success, string Message)> ReopenAsync(string commentId, string userId)
    {
        try
        {
            var comment = await _context.QuarryComments.FindAsync(commentId);
            if (comment == null)
            {
                return (false, "Comment not found");
            }

            comment.IsCompleted = false;
            comment.CompletedDate = null;
            comment.DateModified = DateTime.UtcNow;
            comment.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Comment reopened: {CommentId} | Type: {Type} | By: {UserId}",
                commentId, comment.CommentType, userId);

            return (true, $"{comment.CommentType} reopened!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reopen comment: {CommentId}", commentId);
            return (false, $"Error updating comment: {ex.Message}");
        }
    }

    /// <summary>
    /// Update an existing comment
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateCommentAsync(QuarryComment comment, string userId)
    {
        try
        {
            var existing = await _context.QuarryComments.FindAsync(comment.Id);
            if (existing == null)
            {
                return (false, "Comment not found");
            }

            // Validation
            var validationErrors = ValidateComment(comment);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            // Update fields
            existing.Title = comment.Title;
            existing.Content = comment.Content;
            existing.CommentType = comment.CommentType;
            existing.Priority = comment.Priority;
            existing.DueDate = comment.DueDate;
            existing.LinkedEntityType = comment.LinkedEntityType;
            existing.LinkedEntityId = comment.LinkedEntityId;
            existing.LinkedEntityReference = comment.LinkedEntityReference;
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Comment updated: {CommentId} | Type: {Type} | By: {UserId}",
                comment.Id, existing.CommentType, userId);

            return (true, $"{existing.CommentType} updated successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update comment: {CommentId}", comment.Id);
            return (false, $"Error updating comment: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete a comment
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteCommentAsync(string commentId, string userId)
    {
        try
        {
            var comment = await _context.QuarryComments.FindAsync(commentId);
            if (comment == null)
            {
                return (false, "Comment not found");
            }

            comment.IsActive = false;
            comment.DateModified = DateTime.UtcNow;
            comment.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Comment deleted: {CommentId} | Type: {Type} | By: {UserId}",
                commentId, comment.CommentType, userId);

            return (true, $"{comment.CommentType} deleted successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete comment: {CommentId}", commentId);
            return (false, $"Error deleting comment: {ex.Message}");
        }
    }

    /// <summary>
    /// Get comments linked to a specific entity
    /// </summary>
    public async Task<List<QuarryComment>> GetCommentsForEntityAsync(
        string entityType,
        string entityId,
        string quarryId)
    {
        return await _context.QuarryComments
            .Where(c => c.LinkedEntityType == entityType)
            .Where(c => c.LinkedEntityId == entityId)
            .Where(c => c.QuarryId == quarryId)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.DateCreated)
            .ToListAsync();
    }

    /// <summary>
    /// Validate comment according to business rules
    /// </summary>
    private List<string> ValidateComment(QuarryComment comment)
    {
        var errors = new List<string>();

        // Content is required
        if (string.IsNullOrWhiteSpace(comment.Content))
        {
            errors.Add("Content is required");
        }
        else if (comment.Content.Length > 2000)
        {
            errors.Add("Content cannot exceed 2000 characters");
        }

        // Title max length
        if (!string.IsNullOrEmpty(comment.Title) && comment.Title.Length > 200)
        {
            errors.Add("Title cannot exceed 200 characters");
        }

        // Comment type validation
        var validTypes = new[] { "Note", "Reminder", "Task" };
        if (!string.IsNullOrEmpty(comment.CommentType) && !validTypes.Contains(comment.CommentType))
        {
            errors.Add("Invalid comment type. Must be Note, Reminder, or Task");
        }

        // Priority validation
        var validPriorities = new[] { "Low", "Medium", "High" };
        if (!string.IsNullOrEmpty(comment.Priority) && !validPriorities.Contains(comment.Priority))
        {
            errors.Add("Invalid priority. Must be Low, Medium, or High");
        }

        // Due date validation for reminders and tasks
        if (comment.CommentType is "Reminder" or "Task")
        {
            if (!comment.DueDate.HasValue)
            {
                // Due date is recommended but not required
            }
            else if (comment.DueDate.Value.Date < DateTime.Today.AddDays(-1))
            {
                errors.Add("Due date cannot be more than 1 day in the past");
            }
        }

        return errors;
    }

    /// <summary>
    /// Get available comment types
    /// </summary>
    public static List<(string Value, string Label, string Icon)> GetCommentTypes()
    {
        return new List<(string, string, string)>
        {
            ("Note", "Note", "Notes"),
            ("Reminder", "Reminder", "Alarm"),
            ("Task", "Task", "CheckCircle")
        };
    }

    /// <summary>
    /// Get available priorities
    /// </summary>
    public static List<(string Value, string Label, string Color)> GetPriorities()
    {
        return new List<(string, string, string)>
        {
            ("Low", "Low", "Success"),
            ("Medium", "Medium", "Warning"),
            ("High", "High", "Error")
        };
    }

    /// <summary>
    /// Get available entity types for linking
    /// </summary>
    public static List<(string Value, string Label)> GetEntityTypes()
    {
        return new List<(string, string)>
        {
            ("", "None (Quarry-level)"),
            ("Sale", "Sale"),
            ("Expense", "Expense"),
            ("FuelUsage", "Fuel Usage"),
            ("Banking", "Banking")
        };
    }

    /// <summary>
    /// Get recent notes/comments across multiple quarries (for MainLayout timeline panel)
    /// </summary>
    public async Task<List<QuarryComment>> GetRecentNotesAsync(
        List<string> quarryIds,
        string? commentType = null,
        int limit = 50)
    {
        var query = _context.QuarryComments
            .Where(c => quarryIds.Contains(c.QuarryId))
            .Where(c => c.IsActive)
            .AsQueryable();

        // Filter by comment type if specified
        if (!string.IsNullOrEmpty(commentType))
        {
            query = query.Where(c => c.CommentType == commentType);
        }

        return await query
            .OrderByDescending(c => c.DateCreated)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get count of pending tasks across multiple quarries (for badge display)
    /// </summary>
    public async Task<int> GetPendingTasksCountAsync(List<string> quarryIds)
    {
        return await _context.QuarryComments
            .CountAsync(c => quarryIds.Contains(c.QuarryId)
                && c.IsActive
                && c.CommentType == "Task"
                && !c.IsCompleted);
    }

    /// <summary>
    /// Get count of pending reminders that are due or overdue
    /// </summary>
    public async Task<int> GetDueRemindersCountAsync(List<string> quarryIds)
    {
        var today = DateTime.Today;
        return await _context.QuarryComments
            .CountAsync(c => quarryIds.Contains(c.QuarryId)
                && c.IsActive
                && c.CommentType == "Reminder"
                && !c.IsCompleted
                && c.DueDate.HasValue
                && c.DueDate.Value.Date <= today);
    }
}
