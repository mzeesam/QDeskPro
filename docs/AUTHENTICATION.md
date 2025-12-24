# QDeskPro Authentication System

## Overview

QDeskPro now implements a comprehensive JWT-based authentication system with refresh tokens for persistent login across browser sessions. This replaces the previous cookie-based authentication and enables users to remain logged in even after closing and reopening the browser.

## Key Features

### 1. JWT Token-Based Authentication
- **Access Tokens**: Short-lived tokens (60 minutes) used for API authorization
- **Refresh Tokens**: Long-lived tokens (30 days) stored in database for generating new access tokens
- **Token Rotation**: Automatic rotation of refresh tokens for enhanced security

### 2. Persistent Authentication
- Tokens stored in browser localStorage for persistence across sessions
- Users remain logged in for up to 30 days (refresh token lifetime)
- Automatic token refresh before expiration

### 3. Security Features
- **Cryptographically Secure Tokens**: Uses `RandomNumberGenerator` for token generation
- **Device Tracking**: Records device info and IP address for each token
- **Token Revocation**: Ability to invalidate tokens on logout
- **Audit Trail**: Token replacement chain tracked via `ReplacedByToken`
- **Multi-Device Support**: Can logout from single device or all devices

## Architecture

### Backend Components

#### 1. RefreshToken Entity (`Domain/Entities/RefreshToken.cs`)
```csharp
public class RefreshToken
{
    public string Id { get; set; }                  // Unique identifier
    public string Token { get; set; }               // Cryptographic refresh token
    public string UserId { get; set; }              // Owner user ID
    public DateTime CreatedAt { get; set; }         // Creation timestamp
    public DateTime ExpiresAt { get; set; }         // Expiration timestamp
    public bool IsRevoked { get; set; }             // Revocation flag
    public DateTime? RevokedAt { get; set; }        // Revocation timestamp
    public string? ReplacedByToken { get; set; }    // Audit trail
    public string? DeviceInfo { get; set; }         // Browser/device identifier
    public string? IpAddress { get; set; }          // Client IP address

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;
}
```

#### 2. JwtTokenService (`Domain/Services/JwtTokenService.cs`)

**Key Methods:**

- `GenerateTokensAsync()` - Creates both access and refresh tokens
- `RefreshTokenAsync()` - Validates and rotates refresh tokens
- `RevokeRefreshTokenAsync()` - Revokes single refresh token
- `RevokeAllUserTokensAsync()` - Revokes all user's tokens
- `CleanupExpiredTokensAsync()` - Removes expired tokens from database

**Token Response Model:**
```csharp
public class TokenResponse
{
    public string AccessToken { get; set; }         // JWT access token
    public string RefreshToken { get; set; }        // Refresh token
    public DateTime RefreshTokenExpiry { get; set; } // Expiry date
    public int ExpiresIn { get; set; }              // Seconds until access token expires
    public string TokenType { get; set; }           // "Bearer"
}
```

#### 3. Authentication Endpoints (`Api/Endpoints/AuthEndpoints.cs`)

**Available Endpoints:**

1. **POST /api/auth/login**
   - Authenticates user with email/password
   - Returns access token, refresh token, and user info
   - Request:
     ```json
     {
       "email": "user@example.com",
       "password": "password",
       "rememberMe": true
     }
     ```
   - Response:
     ```json
     {
       "success": true,
       "message": "Login successful",
       "accessToken": "eyJhbGci...",
       "refreshToken": "crypto-secure-token",
       "refreshTokenExpiry": "2025-01-23T12:00:00Z",
       "expiresIn": 3600,
       "user": {
         "id": "user-id",
         "email": "user@example.com",
         "fullName": "John Doe",
         "role": "Clerk",
         "position": "Sales Clerk",
         "quarryId": "quarry-id"
       }
     }
     ```

2. **POST /api/auth/refresh**
   - Refreshes access token using refresh token
   - Request:
     ```json
     {
       "refreshToken": "crypto-secure-token"
     }
     ```
   - Response:
     ```json
     {
       "accessToken": "new-jwt-token",
       "refreshToken": "new-refresh-token",
       "refreshTokenExpiry": "2025-01-23T12:00:00Z",
       "expiresIn": 3600
     }
     ```

3. **POST /api/auth/logout**
   - Revokes refresh token(s)
   - Request:
     ```json
     {
       "refreshToken": "token-to-revoke"  // Optional: if not provided, revokes all user's tokens
     }
     ```
   - Response:
     ```json
     {
       "message": "Logout successful"
     }
     ```

4. **GET /api/auth/me**
   - Gets current authenticated user information
   - Requires: `Authorization: Bearer {access-token}` header
   - Response:
     ```json
     {
       "id": "user-id",
       "email": "user@example.com",
       "fullName": "John Doe",
       "role": "Clerk",
       "position": "Sales Clerk",
       "quarryId": "quarry-id"
     }
     ```

### Frontend Components

#### AuthManager (`wwwroot/js/auth.js`)

JavaScript authentication manager that handles client-side token operations.

**Key Methods:**

1. **Storage Management:**
   - `storeTokens()` - Saves tokens to localStorage
   - `getAccessToken()` - Retrieves access token
   - `getRefreshToken()` - Retrieves refresh token
   - `getUserInfo()` - Gets stored user information
   - `clearTokens()` - Removes all authentication data

2. **Authentication Flow:**
   - `login(email, password)` - Performs login
   - `logout()` - Logs out and revokes tokens
   - `isAuthenticated()` - Checks if user has valid tokens

3. **Token Refresh:**
   - `refreshAccessToken()` - Manually refresh tokens
   - `scheduleTokenRefresh(expiresInSeconds)` - Schedules automatic refresh
   - Automatically refreshes 5 minutes before expiry

4. **API Helper:**
   - `getAuthHeader()` - Returns authorization header object
   - `fetchWithAuth(url, options)` - Makes authenticated API requests with auto-retry on 401

**Auto-Initialization:**
```javascript
// Automatically initializes on page load
window.AuthManager.initialize();
```

## Usage Examples

### Client-Side Login
```javascript
// Login
const result = await window.AuthManager.login('user@example.com', 'password', true);
if (result.success) {
    console.log('Logged in as:', result.user.fullName);
    // Redirect to dashboard or home page
    window.location.href = '/';
} else {
    console.error('Login failed:', result.error);
}
```

### Making Authenticated API Requests
```javascript
// Automatic token refresh on 401
const response = await window.AuthManager.fetchWithAuth('/api/sales', {
    method: 'GET'
});

if (response.ok) {
    const sales = await response.json();
    console.log('Sales:', sales);
}

// Manual token header
const headers = window.AuthManager.getAuthHeader();
// Returns: { 'Authorization': 'Bearer eyJhbGci...' }
```

### Logout
```javascript
// Logout (revokes refresh token)
await window.AuthManager.logout();
window.location.href = '/Account/Login';
```

### Check Authentication Status
```javascript
if (window.AuthManager.isAuthenticated()) {
    console.log('User is logged in');
    const userInfo = window.AuthManager.getUserInfo();
    console.log('User:', userInfo);
} else {
    console.log('User is not logged in');
    window.location.href = '/Account/Login';
}
```

### Server-Side Token Generation (C#)
```csharp
// In your endpoint or service
var user = await userManager.FindByEmailAsync(email);
var roles = await userManager.GetRolesAsync(user);
var role = roles.FirstOrDefault() ?? "Clerk";

// Get device info from request
var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();
var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

// Generate tokens
var tokens = await jwtTokenService.GenerateTokensAsync(
    user,
    role,
    user.QuarryId,
    deviceInfo,
    ipAddress
);

// Return to client
return Results.Ok(new {
    accessToken = tokens.AccessToken,
    refreshToken = tokens.RefreshToken,
    expiresIn = tokens.ExpiresIn
});
```

## Configuration

### appsettings.json
```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key-min-32-chars",
    "Issuer": "QDeskPro",
    "Audience": "QDeskProUsers",
    "ExpirationMinutes": 60,           // Access token: 1 hour
    "RefreshTokenExpirationDays": 30   // Refresh token: 30 days
  }
}
```

## Database Schema

### RefreshTokens Table
```sql
CREATE TABLE RefreshTokens (
    Id NVARCHAR(450) PRIMARY KEY,
    Token NVARCHAR(500) NOT NULL,
    UserId NVARCHAR(450) NOT NULL,
    CreatedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    IsRevoked BIT NOT NULL DEFAULT 0,
    RevokedAt DATETIME2 NULL,
    ReplacedByToken NVARCHAR(MAX) NULL,
    DeviceInfo NVARCHAR(500) NULL,
    IpAddress NVARCHAR(45) NULL,

    CONSTRAINT FK_RefreshTokens_Users_UserId
        FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
);

-- Indexes for performance
CREATE UNIQUE INDEX IX_RefreshTokens_Token ON RefreshTokens(Token);
CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens(UserId);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON RefreshTokens(ExpiresAt);
CREATE INDEX IX_RefreshTokens_UserId_IsRevoked_ExpiresAt
    ON RefreshTokens(UserId, IsRevoked, ExpiresAt);
```

## Security Considerations

### 1. Token Storage
- **Access Token**: Stored in localStorage (short-lived, 60 minutes)
- **Refresh Token**: Stored in localStorage (long-lived, 30 days)
- **Alternative**: For higher security, consider storing refresh token in httpOnly cookie

### 2. Token Rotation
- Every refresh generates a new token pair
- Old refresh token is revoked and linked to new one
- Prevents token reuse attacks

### 3. Device Tracking
- Tracks User-Agent and IP address for each token
- Enables detection of suspicious login patterns
- Supports logout from specific devices

### 4. XSS Protection
- Tokens in localStorage are vulnerable to XSS
- Ensure all user input is properly sanitized
- Use Content Security Policy (CSP) headers

### 5. HTTPS Required
- Always use HTTPS in production
- Tokens transmitted in Authorization header
- Never log tokens in production

### 6. Token Cleanup
- Implement periodic cleanup of expired tokens
- Recommended: Background service running daily
```csharp
// Future enhancement: Create background service
public class TokenCleanupService : IHostedService
{
    private readonly JwtTokenService _tokenService;
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromDays(1));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        await _tokenService.CleanupExpiredTokensAsync();
    }
}
```

## Token Lifecycle

```
1. User Login
   ↓
2. Server generates Access Token (60min) + Refresh Token (30 days)
   ↓
3. Client stores both in localStorage
   ↓
4. Client uses Access Token for API requests
   ↓
5. Access Token expires after 60 minutes
   ↓
6. API returns 401 Unauthorized
   ↓
7. Client automatically calls /api/auth/refresh with Refresh Token
   ↓
8. Server validates Refresh Token
   ↓
9. Server revokes old Refresh Token
   ↓
10. Server generates new Access Token + new Refresh Token
    ↓
11. Client stores new tokens and retries original request
    ↓
12. Cycle repeats until Refresh Token expires (30 days)
    ↓
13. User must login again
```

## Automatic Token Refresh

The `AuthManager` automatically refreshes tokens 5 minutes before expiry:

```javascript
// Scheduled automatically on login
scheduleTokenRefresh(expiresInSeconds) {
    const refreshInMs = (expiresInSeconds * 1000) - REFRESH_BUFFER_MS; // 5 min buffer

    setTimeout(() => {
        this.refreshAccessToken();
    }, refreshInMs);
}
```

## Migration Path

### From Cookie-Based to JWT

For existing installations:

1. **Migration Applied**: `20251224095026_AddRefreshTokens`
2. **Existing Users**: Must login again to get tokens
3. **Backward Compatibility**: Cookie authentication still works for account pages
4. **Gradual Rollout**: Both systems can coexist during transition

## Troubleshooting

### Common Issues

#### 1. "Unauthorized" on every request
- Check if access token is in localStorage
- Verify token hasn't expired
- Check Authorization header format: `Bearer {token}`

#### 2. Token refresh failing
- Verify refresh token in localStorage
- Check if refresh token expired (30 days)
- Look for token in database: `SELECT * FROM RefreshTokens WHERE Token = 'xxx'`

#### 3. User logged out unexpectedly
- Refresh token may have expired
- Token may have been revoked
- Check browser console for errors

#### 4. Tokens not persisting across sessions
- Verify localStorage is enabled in browser
- Check for private/incognito mode
- Ensure auth.js is loaded correctly

### Debug Mode

Enable console logging in auth.js:
```javascript
// All operations log to console with [AuthManager] prefix
console.log('[AuthManager] Initializing...');
console.log('[AuthManager] Tokens stored successfully');
console.log('[AuthManager] Token refreshed successfully');
```

## Future Enhancements

1. **Background Token Cleanup Service**
   - Scheduled job to remove expired tokens
   - Reduce database bloat

2. **Token Revocation on Password Change**
   - Automatically revoke all tokens when password changes
   - Force re-login on all devices

3. **Suspicious Activity Detection**
   - Alert on login from new device/location
   - Require additional verification

4. **Remember Me Enhancement**
   - Extend refresh token lifetime for "Remember Me"
   - Shorter lifetime for public computers

5. **HttpOnly Cookie Option**
   - Store refresh token in httpOnly cookie
   - Enhanced XSS protection
   - Requires separate cookie endpoint

## API Rate Limiting

Authentication endpoints have stricter rate limiting:

```csharp
.RequireRateLimiting("auth")  // Applied to all /api/auth endpoints
```

Configure in Program.cs to prevent brute-force attacks.

## Support

For issues or questions about the authentication system:
- Check application logs: `[INF]` and `[WRN]` messages
- Review browser console for client-side errors
- Inspect database RefreshTokens table for token status

## References

- JWT Standard: [RFC 7519](https://tools.ietf.org/html/rfc7519)
- OAuth 2.0 Refresh Tokens: [RFC 6749](https://tools.ietf.org/html/rfc6749#section-1.5)
- OWASP Token Storage: [OWASP Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/JSON_Web_Token_for_Java_Cheat_Sheet.html)
