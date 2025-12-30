namespace QDeskPro.Features.Admin.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

public class UserService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<UserService> _logger;

    public UserService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<UserService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    // Get all managers (Administrator only)
    public async Task<List<ApplicationUser>> GetManagersAsync()
    {
        try
        {
            var managers = await _userManager.GetUsersInRoleAsync("Manager");
            return managers.OrderBy(m => m.FullName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving managers");
            return new List<ApplicationUser>();
        }
    }

    // Get all clerks (for Manager - filtered by quarry access)
    public async Task<List<ApplicationUser>> GetClerksAsync()
    {
        try
        {
            var clerks = await _userManager.GetUsersInRoleAsync("Clerk");
            return clerks.OrderBy(c => c.FullName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clerks");
            return new List<ApplicationUser>();
        }
    }

    // Get users by quarry (for Manager)
    public async Task<List<ApplicationUser>> GetUsersByQuarryAsync(string quarryId)
    {
        try
        {
            // Get the quarry to find the Manager Owner
            var quarry = await _context.Quarries
                .FirstOrDefaultAsync(q => q.Id == quarryId);

            // Get users assigned via UserQuarries (Clerks and Secondary Managers)
            var assignedUserIds = await _context.UserQuarries
                .Where(uq => uq.QuarryId == quarryId && uq.IsActive)
                .Select(uq => uq.UserId)
                .ToListAsync();

            // Build the list of user IDs to include
            var userIds = new List<string>(assignedUserIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!));

            // Add the Manager Owner if they exist and aren't already in the list
            if (quarry != null && !string.IsNullOrEmpty(quarry.ManagerId))
            {
                if (!userIds.Contains(quarry.ManagerId))
                {
                    userIds.Add(quarry.ManagerId);
                }
            }

            // Get all users
            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id) && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for quarry {QuarryId}", quarryId);
            return new List<ApplicationUser>();
        }
    }

    // Get user by ID
    public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
    {
        return await _userManager.FindByIdAsync(userId);
    }

    // Get user's role
    public async Task<string?> GetUserRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.FirstOrDefault();
    }

    // Get user's quarries
    public async Task<List<Quarry>> GetUserQuarriesAsync(string userId)
    {
        try
        {
            var quarryIds = await _context.UserQuarries
                .Where(uq => uq.UserId == userId && uq.IsActive)
                .Select(uq => uq.QuarryId)
                .ToListAsync();

            var quarries = await _context.Quarries
                .Where(q => quarryIds.Contains(q.Id) && q.IsActive)
                .OrderBy(q => q.QuarryName)
                .ToListAsync();

            return quarries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving quarries for user {UserId}", userId);
            return new List<Quarry>();
        }
    }

    // Create Manager (Administrator or Manager Owner only)
    public async Task<(bool Success, string Message, ApplicationUser? User, string? TempPassword)> CreateManagerAsync(
        string fullName,
        string email,
        string? position,
        string createdBy,
        string? createdByManagerId = null)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Full name is required", null, null);

            if (string.IsNullOrWhiteSpace(email))
                return (false, "Email is required", null, null);

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
                return (false, $"User with email '{email}' already exists", null, null);

            // Generate temporary password using full name
            var tempPassword = GenerateTemporaryPassword(fullName);

            // Create user
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Position = position,
                IsActive = true,
                EmailConfirmed = true, // Auto-confirm for internal users
                CreatedByManagerId = createdByManagerId // Set creator (null for Manager Owners created by Admin)
            };

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to create user: {errors}", null, null);
            }

            // Assign Manager role
            var roleResult = await _userManager.AddToRoleAsync(user, "Manager");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to assign Manager role to user {Email}: {Errors}", email, errors);
                // Don't fail the whole operation, just log it
            }

            var managerType = createdByManagerId == null ? "Manager Owner" : "Secondary Manager";
            _logger.LogInformation("{ManagerType} account created: {Email} by {CreatedBy}", managerType, email, createdBy);

            return (true, "Manager account created successfully", user, tempPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manager account");
            return (false, $"Error creating manager: {ex.Message}", null, null);
        }
    }

    // Create Clerk (Manager only)
    public async Task<(bool Success, string Message, ApplicationUser? User, string? TempPassword)> CreateClerkAsync(
        string fullName,
        string email,
        string? position,
        string primaryQuarryId,
        string createdBy)
    {
        try
        {
            // Validation
            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Full name is required", null, null);

            if (string.IsNullOrWhiteSpace(email))
                return (false, "Email is required", null, null);

            if (string.IsNullOrWhiteSpace(primaryQuarryId))
                return (false, "Primary quarry is required", null, null);

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
                return (false, $"User with email '{email}' already exists", null, null);

            // Verify quarry exists
            var quarry = await _context.Quarries.FindAsync(primaryQuarryId);
            if (quarry == null)
                return (false, "Invalid quarry selected", null, null);

            // Generate temporary password using full name
            var tempPassword = GenerateTemporaryPassword(fullName);

            // Create user
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                Position = position,
                QuarryId = primaryQuarryId,
                IsActive = true,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to create user: {errors}", null, null);
            }

            // Assign Clerk role
            var roleResult = await _userManager.AddToRoleAsync(user, "Clerk");
            if (!roleResult.Succeeded)
            {
                var errors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
                _logger.LogError("Failed to assign Clerk role to user {Email}: {Errors}", email, errors);
            }

            // Auto-assign to primary quarry
            var assignResult = await AssignUserToQuarryAsync(user.Id, primaryQuarryId, true, createdBy);
            if (!assignResult.Success)
            {
                _logger.LogWarning("Failed to auto-assign clerk to quarry: {Message}", assignResult.Message);
            }

            _logger.LogInformation("Clerk account created: {Email} for quarry {QuarryId} by {CreatedBy}",
                email, primaryQuarryId, createdBy);

            return (true, "Clerk account created successfully", user, tempPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating clerk account");
            return (false, $"Error creating clerk: {ex.Message}", null, null);
        }
    }

    // Update user
    public async Task<(bool Success, string Message)> UpdateUserAsync(
        string userId,
        string fullName,
        string? position,
        string modifiedBy)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            user.FullName = fullName;
            user.Position = position;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to update user: {errors}");
            }

            _logger.LogInformation("User {UserId} updated by {ModifiedBy}", userId, modifiedBy);

            return (true, "User updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return (false, $"Error updating user: {ex.Message}");
        }
    }

    // Assign user to quarry
    public async Task<(bool Success, string Message)> AssignUserToQuarryAsync(
        string userId,
        string quarryId,
        bool isPrimary,
        string assignedBy)
    {
        try
        {
            // Check if assignment already exists
            var existingAssignment = await _context.UserQuarries
                .FirstOrDefaultAsync(uq => uq.UserId == userId && uq.QuarryId == quarryId);

            if (existingAssignment != null)
            {
                if (!existingAssignment.IsActive)
                {
                    // Reactivate
                    existingAssignment.IsActive = true;
                    existingAssignment.IsPrimary = isPrimary;
                    existingAssignment.DateModified = DateTime.UtcNow;
                    existingAssignment.ModifiedBy = assignedBy;
                    await _context.SaveChangesAsync();
                    return (true, "User re-assigned to quarry successfully");
                }
                return (false, "User is already assigned to this quarry");
            }

            // If setting as primary, unset other primary assignments
            if (isPrimary)
            {
                var otherPrimary = await _context.UserQuarries
                    .Where(uq => uq.UserId == userId && uq.IsPrimary && uq.IsActive)
                    .ToListAsync();

                foreach (var assignment in otherPrimary)
                {
                    assignment.IsPrimary = false;
                    assignment.DateModified = DateTime.UtcNow;
                    assignment.ModifiedBy = assignedBy;
                }
            }

            // Create new assignment
            var userQuarry = new UserQuarry
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                QuarryId = quarryId,
                IsPrimary = isPrimary,
                QId = quarryId,
                DateStamp = DateTime.Today.ToString("yyyyMMdd"),
                DateCreated = DateTime.UtcNow,
                CreatedBy = assignedBy,
                IsActive = true
            };

            _context.UserQuarries.Add(userQuarry);
            await _context.SaveChangesAsync();

            // Update user's primary QuarryId if this is primary
            if (isPrimary)
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.QuarryId = quarryId;
                    await _userManager.UpdateAsync(user);
                }
            }

            _logger.LogInformation("User {UserId} assigned to quarry {QuarryId} by {AssignedBy}",
                userId, quarryId, assignedBy);

            return (true, "User assigned to quarry successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning user to quarry");
            return (false, $"Error assigning user: {ex.Message}");
        }
    }

    // Remove user from quarry
    public async Task<(bool Success, string Message)> RemoveUserFromQuarryAsync(
        string userId,
        string quarryId,
        string removedBy)
    {
        try
        {
            var assignment = await _context.UserQuarries
                .FirstOrDefaultAsync(uq => uq.UserId == userId && uq.QuarryId == quarryId && uq.IsActive);

            if (assignment == null)
                return (false, "User is not assigned to this quarry");

            // Soft delete
            assignment.IsActive = false;
            assignment.DateModified = DateTime.UtcNow;
            assignment.ModifiedBy = removedBy;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} removed from quarry {QuarryId} by {RemovedBy}",
                userId, quarryId, removedBy);

            return (true, "User removed from quarry successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing user from quarry");
            return (false, $"Error removing user: {ex.Message}");
        }
    }

    // Deactivate user
    public async Task<(bool Success, string Message)> DeactivateUserAsync(string userId, string deactivatedBy)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            if (!user.IsActive)
                return (false, "User is already inactive");

            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to deactivate user: {errors}");
            }

            // Deactivate all quarry assignments
            var assignments = await _context.UserQuarries
                .Where(uq => uq.UserId == userId && uq.IsActive)
                .ToListAsync();

            foreach (var assignment in assignments)
            {
                assignment.IsActive = false;
                assignment.DateModified = DateTime.UtcNow;
                assignment.ModifiedBy = deactivatedBy;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} deactivated by {DeactivatedBy}", userId, deactivatedBy);

            return (true, "User deactivated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating user");
            return (false, $"Error deactivating user: {ex.Message}");
        }
    }

    // Reactivate user
    public async Task<(bool Success, string Message)> ReactivateUserAsync(string userId, string reactivatedBy)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return (false, "User not found");

            if (user.IsActive)
                return (false, "User is already active");

            user.IsActive = true;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to reactivate user: {errors}");
            }

            _logger.LogInformation("User {UserId} reactivated by {ReactivatedBy}", userId, reactivatedBy);

            return (true, "User reactivated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating user");
            return (false, $"Error reactivating user: {ex.Message}");
        }
    }

    // Reset user password (generates new temporary password)
    public async Task<(bool Success, string Message, string? TempPassword)> ResetPasswordAsync(
        string userId,
        string resetBy)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return (false, "User not found", null);

            // Generate new temporary password using full name
            var tempPassword = GenerateTemporaryPassword(user.FullName ?? "User");

            // Remove current password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, tempPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, $"Failed to reset password: {errors}", null);
            }

            _logger.LogInformation("Password reset for user {UserId} by {ResetBy}", userId, resetBy);

            return (true, "Password reset successfully", tempPassword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return (false, $"Error resetting password: {ex.Message}", null);
        }
    }

    // Helper: Generate temporary password
    // Format: {FirstName}.{LastName}@{currentyear}
    // Example: "John Doe" -> "John.Doe@2025"
    // Meets requirements: uppercase, lowercase, digit, non-alphanumeric, 12+ chars
    private string GenerateTemporaryPassword(string fullName)
    {
        // Split name into parts
        var nameParts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        string formattedName;
        if (nameParts.Length >= 2)
        {
            // Join first and last name with a dot
            formattedName = $"{nameParts[0]}.{nameParts[^1]}";
        }
        else if (nameParts.Length == 1)
        {
            // Single name - duplicate it
            formattedName = $"{nameParts[0]}.User";
        }
        else
        {
            formattedName = "New.User";
        }

        // Get current year
        var currentYear = DateTime.Now.Year;

        // Combine with @ symbol (non-alphanumeric) and year
        var password = $"{formattedName}@{currentYear}";

        // Ensure minimum length of 12 characters
        if (password.Length < 12)
        {
            password = $"{formattedName}@{currentYear}!";
        }

        return password;
    }

    // Get user's quarry context (for Clerk pages)
    public async Task<UserQuarryContext?> GetUserQuarryContextAsync(string userId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.QuarryAssignments)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return null;
            }

            // Get primary quarry - first try from ApplicationUser.QuarryId
            string? quarryId = user.QuarryId;

            // If not set, try to get from UserQuarries where IsPrimary = true
            if (string.IsNullOrEmpty(quarryId))
            {
                var primaryAssignment = await _context.UserQuarries
                    .Where(uq => uq.UserId == userId && uq.IsPrimary && uq.IsActive)
                    .FirstOrDefaultAsync();

                quarryId = primaryAssignment?.QuarryId;
            }

            // If still not found, get the first active quarry assignment
            if (string.IsNullOrEmpty(quarryId))
            {
                var firstAssignment = await _context.UserQuarries
                    .Where(uq => uq.UserId == userId && uq.IsActive)
                    .OrderBy(uq => uq.DateCreated)
                    .FirstOrDefaultAsync();

                quarryId = firstAssignment?.QuarryId;
            }

            if (string.IsNullOrEmpty(quarryId))
            {
                _logger.LogWarning("No quarry assignment found for user: {UserId}", userId);
                return null;
            }

            // Load quarry details
            var quarry = await _context.Quarries
                .FirstOrDefaultAsync(q => q.Id == quarryId && q.IsActive);

            if (quarry == null)
            {
                _logger.LogWarning("Quarry not found or inactive: {QuarryId}", quarryId);
                return null;
            }

            return new UserQuarryContext
            {
                UserId = userId,
                UserFullName = user.FullName ?? "",
                UserPosition = user.Position ?? "Clerk",
                QuarryId = quarry.Id,
                QuarryName = quarry.QuarryName ?? "",
                Quarry = quarry
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting quarry context for user {UserId}", userId);
            return null;
        }
    }

    // Validation: Check if user can manage target user
    public async Task<bool> CanManageUserAsync(string managerId, string targetUserId)
    {
        var manager = await _userManager.FindByIdAsync(managerId);
        if (manager == null) return false;

        var managerRoles = await _userManager.GetRolesAsync(manager);
        var managerRole = managerRoles.FirstOrDefault();

        var targetUser = await _userManager.FindByIdAsync(targetUserId);
        if (targetUser == null) return false;

        var targetRoles = await _userManager.GetRolesAsync(targetUser);
        var targetRole = targetRoles.FirstOrDefault();

        // Administrator can manage all Managers
        if (managerRole == "Administrator" && targetRole == "Manager")
            return true;

        // Manager Owner can manage Secondary Managers they created
        if (managerRole == "Manager" && targetRole == "Manager")
        {
            // Check if targetUser was created by this manager (Manager Owner managing Secondary Manager)
            return targetUser.CreatedByManagerId == managerId;
        }

        // Manager can manage Clerks assigned to their quarries
        if (managerRole == "Manager" && targetRole == "Clerk")
        {
            // Check if clerk is assigned to any of manager's quarries (owned or assigned)
            var managerQuarryIds = await _context.Quarries
                .Where(q => q.ManagerId == managerId && q.IsActive)
                .Select(q => q.Id)
                .ToListAsync();

            var clerkQuarryIds = await _context.UserQuarries
                .Where(uq => uq.UserId == targetUserId && uq.IsActive)
                .Select(uq => uq.QuarryId)
                .ToListAsync();

            return managerQuarryIds.Intersect(clerkQuarryIds).Any();
        }

        return false;
    }

    // Check if user is a Manager Owner (created by Admin, not by another manager)
    public async Task<bool> IsManagerOwnerAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return false;

            var roles = await _userManager.GetRolesAsync(user);

            // User must have Manager role AND CreatedByManagerId must be null
            return roles.Contains("Manager") && user.CreatedByManagerId == null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is a Manager Owner", userId);
            return false;
        }
    }

    // Get Secondary Managers created by a Manager Owner
    public async Task<List<ApplicationUser>> GetSecondaryManagersAsync(string managerOwnerId)
    {
        try
        {
            return await _context.Users
                .Where(u => u.CreatedByManagerId == managerOwnerId && u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Secondary Managers for Manager Owner {ManagerOwnerId}", managerOwnerId);
            return new List<ApplicationUser>();
        }
    }

    // Get quarries owned by a Manager (for Manager Owners)
    public async Task<List<Quarry>> GetOwnedQuarriesAsync(string managerId)
    {
        try
        {
            return await _context.Quarries
                .Where(q => q.ManagerId == managerId && q.IsActive)
                .OrderBy(q => q.QuarryName)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving owned quarries for Manager {ManagerId}", managerId);
            return new List<Quarry>();
        }
    }

    // Get all accessible quarries for a manager (owned + assigned)
    public async Task<List<Quarry>> GetAccessibleQuarriesAsync(string managerId)
    {
        try
        {
            // Get quarries owned by this manager
            var ownedQuarries = await _context.Quarries
                .Where(q => q.ManagerId == managerId && q.IsActive)
                .ToListAsync();

            // Get quarries assigned to this manager (for Secondary Managers)
            var assignedQuarryIds = await _context.UserQuarries
                .Where(uq => uq.UserId == managerId && uq.IsActive)
                .Select(uq => uq.QuarryId)
                .ToListAsync();

            var assignedQuarries = await _context.Quarries
                .Where(q => assignedQuarryIds.Contains(q.Id) && q.IsActive)
                .ToListAsync();

            // Combine and remove duplicates
            var allQuarries = ownedQuarries
                .Concat(assignedQuarries)
                .GroupBy(q => q.Id)
                .Select(g => g.First())
                .OrderBy(q => q.QuarryName)
                .ToList();

            return allQuarries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accessible quarries for Manager {ManagerId}", managerId);
            return new List<Quarry>();
        }
    }
}

/// <summary>
/// User quarry context for Clerk operations
/// </summary>
public class UserQuarryContext
{
    public string UserId { get; set; } = "";
    public string UserFullName { get; set; } = "";
    public string UserPosition { get; set; } = "";
    public string QuarryId { get; set; } = "";
    public string QuarryName { get; set; } = "";
    public Quarry? Quarry { get; set; }
}
