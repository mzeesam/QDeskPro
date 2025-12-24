using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Services;

namespace QDeskPro.Api.Endpoints;

public static class JwtAuthEndpoints
{
    public static void MapJwtAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/jwt-auth").WithTags("JWT Authentication");

        group.MapPost("/login", Login)
            .AllowAnonymous()
            .Produces<JwtLoginResponse>(200)
            .Produces<ProblemDetails>(400);

        group.MapPost("/register", Register)
            .AllowAnonymous()
            .Produces<JwtLoginResponse>(200)
            .Produces<ProblemDetails>(400);

        group.MapPost("/refresh", RefreshToken)
            .AllowAnonymous()
            .Produces<JwtLoginResponse>(200)
            .Produces<ProblemDetails>(400);

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .Produces(200);
    }

    private static async Task<IResult> Login(
        JwtLoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        JwtTokenService jwtTokenService,
        AppDbContext context,
        HttpContext httpContext)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        if (user == null)
        {
            return Results.BadRequest(new { message = "Invalid email or password" });
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return Results.BadRequest(new { message = "Account locked out. Please try again later." });
            }

            return Results.BadRequest(new { message = "Invalid email or password" });
        }

        // Get user role
        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        // Get quarry ID for clerks
        string? quarryId = null;
        if (role == "Clerk")
        {
            var userQuarry = await context.UserQuarries
                .Where(uq => uq.UserId == user.Id && uq.IsPrimary)
                .FirstOrDefaultAsync();

            quarryId = userQuarry?.QuarryId;
        }

        // Get device info and IP address
        var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        // Generate access and refresh tokens
        var tokenResponse = await jwtTokenService.GenerateTokensAsync(user, role, quarryId, deviceInfo, ipAddress);

        return Results.Ok(new JwtLoginResponse
        {
            Token = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            RefreshTokenExpiry = tokenResponse.RefreshTokenExpiry,
            ExpiresIn = tokenResponse.ExpiresIn,
            Email = user.Email!,
            FullName = user.FullName,
            Role = role,
            QuarryId = quarryId
        });
    }

    private static async Task<IResult> Register(
        JwtRegisterRequest request,
        UserManager<ApplicationUser> userManager,
        JwtTokenService jwtTokenService,
        HttpContext httpContext)
    {
        var existingUser = await userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Results.BadRequest(new { message = "Email already registered" });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            EmailConfirmed = true // Auto-confirm for now
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Results.BadRequest(new { message = errors });
        }

        // Assign default role (Clerk)
        await userManager.AddToRoleAsync(user, "Clerk");

        // Get device info and IP address
        var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        // Generate access and refresh tokens
        var tokenResponse = await jwtTokenService.GenerateTokensAsync(user, "Clerk", null, deviceInfo, ipAddress);

        return Results.Ok(new JwtLoginResponse
        {
            Token = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            RefreshTokenExpiry = tokenResponse.RefreshTokenExpiry,
            ExpiresIn = tokenResponse.ExpiresIn,
            Email = user.Email!,
            FullName = user.FullName,
            Role = "Clerk",
            QuarryId = null
        });
    }

    private static async Task<IResult> RefreshToken(
        RefreshTokenRequest request,
        JwtTokenService jwtTokenService,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.BadRequest(new { message = "Refresh token is required" });
        }

        // Get device info and IP address
        var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        // Refresh the token
        var tokenResponse = await jwtTokenService.RefreshTokenAsync(request.RefreshToken, deviceInfo, ipAddress);

        if (tokenResponse == null)
        {
            return Results.BadRequest(new { message = "Invalid or expired refresh token" });
        }

        return Results.Ok(new JwtLoginResponse
        {
            Token = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            RefreshTokenExpiry = tokenResponse.RefreshTokenExpiry,
            ExpiresIn = tokenResponse.ExpiresIn,
            Email = "", // Not needed for refresh
            FullName = "",
            Role = "",
            QuarryId = null
        });
    }

    private static async Task<IResult> Logout(
        RefreshTokenRequest request,
        JwtTokenService jwtTokenService)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            await jwtTokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }

        return Results.Ok(new { message = "Logged out successfully" });
    }
}

public record JwtLoginRequest(string Email, string Password, bool RememberMe = true);

public record JwtRegisterRequest(string Email, string Password, string FullName);

public record JwtLoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiry { get; set; }
    public int ExpiresIn { get; set; } // Seconds until access token expires
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? QuarryId { get; set; }
}
