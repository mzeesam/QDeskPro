using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting("auth");  // Stricter rate limiting for auth endpoints

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithDescription("Authenticate user with email and password")
            .AllowAnonymous();

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithDescription("Refresh access token using refresh token")
            .AllowAnonymous();

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithDescription("Sign out and revoke refresh token")
            .RequireAuthorization();

        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithDescription("Get current authenticated user information")
            .RequireAuthorization();
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest request,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        JwtTokenService tokenService,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Email and password are required" });
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Results.Unauthorized();
        }

        if (!user.IsActive)
        {
            return Results.BadRequest(new { message = "Account is deactivated. Please contact administrator." });
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var roles = await userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Clerk";

            // Get device info and IP address for security tracking
            var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            // Generate JWT tokens
            var tokens = await tokenService.GenerateTokensAsync(user, role, user.QuarryId, deviceInfo, ipAddress);

            return Results.Ok(new LoginResponse(
                Success: true,
                Message: "Login successful",
                AccessToken: tokens.AccessToken,
                RefreshToken: tokens.RefreshToken,
                RefreshTokenExpiry: tokens.RefreshTokenExpiry,
                ExpiresIn: tokens.ExpiresIn,
                User: new UserInfo(
                    Id: user.Id,
                    Email: user.Email!,
                    FullName: user.FullName,
                    Role: role,
                    Position: user.Position,
                    QuarryId: user.QuarryId
                )
            ));
        }

        if (result.IsLockedOut)
        {
            return Results.BadRequest(new { message = "Account locked due to multiple failed login attempts. Please try again later." });
        }

        if (result.IsNotAllowed)
        {
            return Results.BadRequest(new { message = "Email not confirmed. Please check your email for confirmation link." });
        }

        return Results.Unauthorized();
    }

    private static async Task<IResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        JwtTokenService tokenService,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Results.BadRequest(new { message = "Refresh token is required" });
        }

        // Get device info and IP address for security tracking
        var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

        // Refresh the tokens
        var tokens = await tokenService.RefreshTokenAsync(request.RefreshToken, deviceInfo, ipAddress);

        if (tokens == null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new RefreshTokenResponse(
            AccessToken: tokens.AccessToken,
            RefreshToken: tokens.RefreshToken,
            RefreshTokenExpiry: tokens.RefreshTokenExpiry,
            ExpiresIn: tokens.ExpiresIn
        ));
    }

    private static async Task<IResult> Logout(
        [FromBody] LogoutRequest request,
        JwtTokenService tokenService,
        ClaimsPrincipal user)
    {
        // Get user ID from claims
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrEmpty(request.RefreshToken))
        {
            // Revoke the specific refresh token
            await tokenService.RevokeRefreshTokenAsync(request.RefreshToken);
        }
        else if (!string.IsNullOrEmpty(userId))
        {
            // If no specific token provided, revoke all user's tokens (logout from all devices)
            await tokenService.RevokeAllUserTokensAsync(userId);
        }

        return Results.Ok(new { message = "Logout successful" });
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var appUser = await userManager.FindByIdAsync(userId);
        if (appUser == null || !appUser.IsActive)
        {
            return Results.NotFound(new { message = "User not found or deactivated" });
        }

        var roles = await userManager.GetRolesAsync(appUser);
        var role = roles.FirstOrDefault() ?? "Clerk";

        return Results.Ok(new UserInfo(
            Id: appUser.Id,
            Email: appUser.Email!,
            FullName: appUser.FullName,
            Role: role,
            Position: appUser.Position,
            QuarryId: appUser.QuarryId
        ));
    }
}

public record LoginRequest(
    string Email,
    string Password,
    bool RememberMe = false
);

public record LoginResponse(
    bool Success,
    string Message,
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiry,
    int ExpiresIn,
    UserInfo? User = null
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiry,
    int ExpiresIn
);

public record LogoutRequest(
    string? RefreshToken = null
);

public record UserInfo(
    string Id,
    string Email,
    string FullName,
    string Role,
    string? Position = null,
    string? QuarryId = null
);
