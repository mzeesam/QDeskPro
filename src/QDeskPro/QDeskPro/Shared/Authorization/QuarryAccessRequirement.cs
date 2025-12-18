using Microsoft.AspNetCore.Authorization;

namespace QDeskPro.Shared.Authorization;

/// <summary>
/// Authorization requirement for quarry-level access.
/// </summary>
public class QuarryAccessRequirement : IAuthorizationRequirement
{
}
