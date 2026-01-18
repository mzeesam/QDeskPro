using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using QDeskPro.Domain.Services;

namespace QDeskPro.Shared.Services;

/// <summary>
/// Custom authentication state provider that uses JWT tokens with proactive refresh.
/// ROLLBACK: If issues arise, revert to the previous version without proactive refresh.
/// </summary>
public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly LocalStorageService _localStorage;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    // Proactive refresh: refresh 5 minutes before expiration
    private const int PROACTIVE_REFRESH_MINUTES = 5;

    public CustomAuthenticationStateProvider(
        LocalStorageService localStorage,
        JwtTokenService jwtTokenService,
        ILogger<CustomAuthenticationStateProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _localStorage = localStorage;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync("authToken");

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogDebug("No auth token found in localStorage");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            // PROACTIVE REFRESH: Check if token is about to expire
            var tokenExpiration = GetTokenExpiration(token);
            if (tokenExpiration.HasValue)
            {
                var timeUntilExpiry = tokenExpiration.Value - DateTimeOffset.UtcNow;

                // If token expires within PROACTIVE_REFRESH_MINUTES, refresh proactively
                if (timeUntilExpiry.TotalMinutes <= PROACTIVE_REFRESH_MINUTES && timeUntilExpiry.TotalMinutes > 0)
                {
                    _logger.LogInformation("Token expiring in {Minutes:F1} minutes, proactively refreshing", timeUntilExpiry.TotalMinutes);
                    var refreshed = await TryRefreshTokenAsync();
                    if (refreshed)
                    {
                        token = await _localStorage.GetItemAsync("authToken");
                    }
                }
            }

            var principal = _jwtTokenService.ValidateToken(token!);

            if (principal == null)
            {
                _logger.LogWarning("Access token validation failed, attempting refresh");

                // Try to refresh the token
                var refreshed = await TryRefreshTokenAsync();
                if (refreshed)
                {
                    // Get the new token and validate it
                    var newToken = await _localStorage.GetItemAsync("authToken");
                    var newPrincipal = _jwtTokenService.ValidateToken(newToken!);

                    if (newPrincipal != null)
                    {
                        _logger.LogInformation("Successfully refreshed access token");
                        return new AuthenticationState(newPrincipal);
                    }
                }

                // Refresh failed, clear all tokens
                _logger.LogWarning("Token refresh failed, clearing all tokens");
                await ClearTokensAsync();
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            _logger.LogDebug("User authenticated from token");
            return new AuthenticationState(principal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authentication state");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    /// <summary>
    /// Parse JWT token to get expiration time without full validation
    /// </summary>
    private DateTimeOffset? GetTokenExpiration(string token)
    {
        try
        {
            var tokenParts = token.Split('.');
            if (tokenParts.Length != 3) return null;

            var payload = tokenParts[1];
            // Add padding if needed for base64 decoding
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var expSeconds = expElement.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing token expiration");
            return null;
        }
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync("refreshToken");

            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogDebug("No refresh token available");
                return false;
            }

            // Check if refresh token has expired
            var refreshTokenExpiryStr = await _localStorage.GetItemAsync("refreshTokenExpiry");
            if (!string.IsNullOrEmpty(refreshTokenExpiryStr))
            {
                if (DateTime.TryParse(refreshTokenExpiryStr, out var refreshTokenExpiry))
                {
                    if (refreshTokenExpiry < DateTime.UtcNow)
                    {
                        _logger.LogWarning("Refresh token has expired");
                        return false;
                    }
                }
            }

            // Call refresh endpoint
            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.PostAsJsonAsync("/api/jwt-auth/refresh", new { RefreshToken = refreshToken });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();

                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    // Store new tokens
                    await _localStorage.SetItemAsync("authToken", result.Token);

                    if (!string.IsNullOrEmpty(result.RefreshToken))
                    {
                        await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
                        await _localStorage.SetItemAsync("refreshTokenExpiry", result.RefreshTokenExpiry.ToString("O"));
                    }

                    _logger.LogInformation("Tokens refreshed successfully");
                    return true;
                }
            }
            else
            {
                _logger.LogWarning("Refresh token request failed with status: {StatusCode}", response.StatusCode);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return false;
        }
    }

    private async Task ClearTokensAsync()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        await _localStorage.RemoveItemAsync("refreshTokenExpiry");
    }

    public async Task MarkUserAsAuthenticated(string token)
    {
        await _localStorage.SetItemAsync("authToken", token);

        var principal = _jwtTokenService.ValidateToken(token);

        if (principal != null)
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
        }
    }

    public async Task MarkUserAsLoggedOut()
    {
        // Clear all tokens
        await ClearTokensAsync();

        // Also clear user info
        await _localStorage.RemoveItemAsync("userEmail");
        await _localStorage.RemoveItemAsync("userFullName");
        await _localStorage.RemoveItemAsync("userRole");
        await _localStorage.RemoveItemAsync("userQuarryId");

        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymousUser)));
    }

    private record RefreshResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiry { get; set; }
        public int ExpiresIn { get; set; }
    }
}
