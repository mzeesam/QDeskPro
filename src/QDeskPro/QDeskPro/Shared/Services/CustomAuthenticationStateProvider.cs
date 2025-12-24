using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using QDeskPro.Domain.Services;

namespace QDeskPro.Shared.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly LocalStorageService _localStorage;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<CustomAuthenticationStateProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

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

            var principal = _jwtTokenService.ValidateToken(token);

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
