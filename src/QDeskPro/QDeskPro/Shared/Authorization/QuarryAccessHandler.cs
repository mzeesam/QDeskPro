using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;

namespace QDeskPro.Shared.Authorization;

/// <summary>
/// Authorization handler for quarry-level resource access.
/// </summary>
public class QuarryAccessHandler : AuthorizationHandler<QuarryAccessRequirement, string>
{
    private readonly IServiceProvider _serviceProvider;

    public QuarryAccessHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        QuarryAccessRequirement requirement,
        string quarryId)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // Administrators have access to all quarries
        if (userRole == "Administrator")
        {
            context.Succeed(requirement);
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Managers have access to quarries they own or are assigned to
        if (userRole == "Manager")
        {
            var hasAccess = await db.Quarries.AnyAsync(q =>
                q.Id == quarryId && q.ManagerId == userId) ||
                await db.UserQuarries.AnyAsync(uq =>
                    uq.UserId == userId && uq.QuarryId == quarryId);

            if (hasAccess) 
                context.Succeed(requirement);
            return;
        }

        // Clerks have access only to assigned quarries
        if (userRole == "Clerk")
        {
            var hasAccess = await db.UserQuarries.AnyAsync(uq =>
                uq.UserId == userId && uq.QuarryId == quarryId);

            if (hasAccess) 
                context.Succeed(requirement);
        }
    }
}
