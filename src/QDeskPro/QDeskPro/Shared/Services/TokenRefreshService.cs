using System.Timers;
using Microsoft.AspNetCore.Components.Authorization;

namespace QDeskPro.Shared.Services;

/// <summary>
/// Background service that proactively refreshes JWT tokens before they expire.
/// This prevents session drops caused by expired tokens.
///
/// ROLLBACK: To disable, comment out the service registration in Program.cs:
/// builder.Services.AddScoped&lt;TokenRefreshService&gt;();
/// </summary>
public class TokenRefreshService : IDisposable
{
    private readonly LocalStorageService _localStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TokenRefreshService> _logger;
    private readonly AuthenticationStateProvider _authStateProvider;

    private System.Timers.Timer? _refreshTimer;
    private bool _isInitialized = false;
    private bool _disposed = false;

    // Refresh tokens 5 minutes before they expire
    private const int REFRESH_BUFFER_MINUTES = 5;
    // Check token expiration every 2 minutes
    private const int CHECK_INTERVAL_MINUTES = 2;

    public TokenRefreshService(
        LocalStorageService localStorage,
        IHttpClientFactory httpClientFactory,
        ILogger<TokenRefreshService> logger,
        AuthenticationStateProvider authStateProvider)
    {
        _localStorage = localStorage;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _authStateProvider = authStateProvider;
    }

    /// <summary>
    /// Initialize the token refresh timer. Call this after user login.
    /// </summary>
    public void StartRefreshTimer()
    {
        if (_isInitialized || _disposed) return;

        _refreshTimer = new System.Timers.Timer(TimeSpan.FromMinutes(CHECK_INTERVAL_MINUTES).TotalMilliseconds);
        _refreshTimer.Elapsed += async (sender, e) => await CheckAndRefreshTokenAsync();
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();

        _isInitialized = true;
        _logger.LogInformation("Token refresh timer started (checking every {Minutes} minutes)", CHECK_INTERVAL_MINUTES);
    }

    /// <summary>
    /// Stop the token refresh timer. Call this on user logout.
    /// </summary>
    public void StopRefreshTimer()
    {
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;
        _isInitialized = false;
        _logger.LogInformation("Token refresh timer stopped");
    }

    /// <summary>
    /// Check if token needs refresh and refresh if necessary
    /// </summary>
    public async Task CheckAndRefreshTokenAsync()
    {
        try
        {
            var refreshTokenExpiryStr = await _localStorage.GetItemAsync("refreshTokenExpiry");
            var authToken = await _localStorage.GetItemAsync("authToken");

            if (string.IsNullOrEmpty(authToken) || string.IsNullOrEmpty(refreshTokenExpiryStr))
            {
                _logger.LogDebug("No tokens found, skipping refresh check");
                return;
            }

            // Check if we need to refresh based on access token expiration
            // Access tokens expire in 60 minutes, so refresh 5 minutes before
            var shouldRefresh = await ShouldRefreshTokenAsync();

            if (shouldRefresh)
            {
                _logger.LogInformation("Token expiring soon, initiating proactive refresh");
                await RefreshTokenAsync();
            }
        }
        catch (Exception ex)
        {
            // Don't crash the timer on errors - just log and continue
            _logger.LogWarning(ex, "Error during token refresh check");
        }
    }

    /// <summary>
    /// Determine if we should refresh the token (5 minutes before expiration)
    /// </summary>
    private async Task<bool> ShouldRefreshTokenAsync()
    {
        try
        {
            var authToken = await _localStorage.GetItemAsync("authToken");
            if (string.IsNullOrEmpty(authToken)) return false;

            // Parse JWT to get expiration time
            var tokenParts = authToken.Split('.');
            if (tokenParts.Length != 3) return true; // Invalid token, try refresh

            var payload = tokenParts[1];
            // Add padding if needed
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var payloadBytes = Convert.FromBase64String(payload);
            var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

            using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var expSeconds = expElement.GetInt64();
                var expTime = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                var now = DateTimeOffset.UtcNow;
                var timeUntilExpiry = expTime - now;

                _logger.LogDebug("Token expires in {Minutes} minutes", timeUntilExpiry.TotalMinutes);

                // Refresh if less than REFRESH_BUFFER_MINUTES minutes until expiry
                return timeUntilExpiry.TotalMinutes <= REFRESH_BUFFER_MINUTES;
            }

            return true; // No exp claim, refresh to be safe
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing token expiration, will attempt refresh");
            return true; // Error parsing, try refresh
        }
    }

    /// <summary>
    /// Refresh the access token using the refresh token
    /// </summary>
    private async Task RefreshTokenAsync()
    {
        try
        {
            var refreshToken = await _localStorage.GetItemAsync("refreshToken");

            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No refresh token available for proactive refresh");
                return;
            }

            // Check if refresh token has expired
            var refreshTokenExpiryStr = await _localStorage.GetItemAsync("refreshTokenExpiry");
            if (!string.IsNullOrEmpty(refreshTokenExpiryStr))
            {
                if (DateTime.TryParse(refreshTokenExpiryStr, out var refreshTokenExpiry))
                {
                    if (refreshTokenExpiry < DateTime.UtcNow)
                    {
                        _logger.LogWarning("Refresh token has expired, user needs to re-login");
                        return;
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

                    // Notify authentication state provider
                    if (_authStateProvider is CustomAuthenticationStateProvider customAuth)
                    {
                        await customAuth.MarkUserAsAuthenticated(result.Token);
                    }

                    _logger.LogInformation("Tokens refreshed proactively, new token expires in {Seconds} seconds", result.ExpiresIn);
                }
            }
            else
            {
                _logger.LogWarning("Proactive token refresh failed with status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during proactive token refresh");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        StopRefreshTimer();
        _disposed = true;
    }

    private record RefreshResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiry { get; set; }
        public int ExpiresIn { get; set; }
    }
}
