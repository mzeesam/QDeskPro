using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Shared.Models;

namespace QDeskPro.Domain.Services;

public class JwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public JwtTokenService(
        IOptions<JwtSettings> jwtSettings,
        ILogger<JwtTokenService> logger,
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
        _context = context;
        _userManager = userManager;
    }

    public string GenerateToken(ApplicationUser user, string role, string? quarryId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Name, user.FullName ?? ""),
            new(ClaimTypes.Role, role),
            new("FullName", user.FullName ?? ""),
            new("Position", user.Position ?? "")
        };

        if (!string.IsNullOrEmpty(quarryId))
        {
            claims.Add(new Claim("QuarryId", quarryId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: credentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("Generated JWT token for user {UserId} with role {Role}", user.Id, role);

        return tokenString;
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Token validation failed");
            return null;
        }
    }

    /// <summary>
    /// Generate both access token and refresh token for a user
    /// </summary>
    public async Task<TokenResponse> GenerateTokensAsync(ApplicationUser user, string role, string? quarryId = null, string? deviceInfo = null, string? ipAddress = null)
    {
        // Generate access token
        var accessToken = GenerateToken(user, role, quarryId);

        // Generate refresh token
        var refreshToken = GenerateRefreshToken();
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);

        // Store refresh token in database
        var refreshTokenEntity = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = refreshTokenExpiry,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };

        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Generated tokens for user {UserId} with device {Device}",
            user.Id, deviceInfo ?? "Unknown");

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiry = refreshTokenExpiry,
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60, // Convert to seconds
            TokenType = "Bearer"
        };
    }

    /// <summary>
    /// Generate cryptographically secure refresh token
    /// </summary>
    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    /// <summary>
    /// Refresh access token using valid refresh token
    /// </summary>
    public async Task<TokenResponse?> RefreshTokenAsync(string refreshToken, string? deviceInfo = null, string? ipAddress = null)
    {
        // Find refresh token in database
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && rt.IsActive);

        if (storedToken == null)
        {
            _logger.LogWarning("Refresh token not found or expired");
            return null;
        }

        // Get user and role
        var user = storedToken.User;
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Clerk";

        // Revoke old refresh token
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var newTokens = await GenerateTokensAsync(user, role, user.QuarryId, deviceInfo, ipAddress);

        // Link old token to new one for audit trail
        storedToken.ReplacedByToken = newTokens.RefreshToken;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Refreshed tokens for user {UserId}", user.Id);

        return newTokens;
    }

    /// <summary>
    /// Revoke refresh token (logout)
    /// </summary>
    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null)
        {
            return false;
        }

        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Revoked refresh token for user {UserId}", storedToken.UserId);

        return true;
    }

    /// <summary>
    /// Revoke all refresh tokens for a user (logout from all devices)
    /// </summary>
    public async Task RevokeAllUserTokensAsync(string userId)
    {
        var userTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync();

        foreach (var token in userTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Revoked all tokens for user {UserId}", userId);
    }

    /// <summary>
    /// Clean up expired refresh tokens (should be run periodically)
    /// </summary>
    public async Task CleanupExpiredTokensAsync()
    {
        var expiredTokens = await _context.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(expiredTokens);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} expired refresh tokens", expiredTokens.Count);
    }
}

/// <summary>
/// Token response model with access and refresh tokens
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiry { get; set; }
    public int ExpiresIn { get; set; } // Seconds until access token expires
    public string TokenType { get; set; } = "Bearer";
}
