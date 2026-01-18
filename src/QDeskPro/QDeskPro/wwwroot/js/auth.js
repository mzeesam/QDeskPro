/**
 * QDeskPro Authentication Manager
 * Handles JWT token storage, refresh, and automatic authentication
 */
window.AuthManager = {
    // Storage keys - MUST match keys used in Blazor Login.razor and CustomAuthenticationStateProvider
    ACCESS_TOKEN_KEY: 'authToken',
    REFRESH_TOKEN_KEY: 'refreshToken',
    REFRESH_EXPIRY_KEY: 'refreshTokenExpiry',
    USER_INFO_KEY: 'userInfo',

    // Refresh token 5 minutes before expiry
    REFRESH_BUFFER_MS: 5 * 60 * 1000,

    // Refresh timer
    refreshTimer: null,

    /**
     * Store authentication tokens and user info in localStorage
     */
    storeTokens: function(accessToken, refreshToken, refreshTokenExpiry, expiresIn, userInfo) {
        try {
            localStorage.setItem(this.ACCESS_TOKEN_KEY, accessToken);
            localStorage.setItem(this.REFRESH_TOKEN_KEY, refreshToken);
            localStorage.setItem(this.REFRESH_EXPIRY_KEY, refreshTokenExpiry);

            if (userInfo) {
                localStorage.setItem(this.USER_INFO_KEY, JSON.stringify(userInfo));
            }

            // Schedule automatic refresh
            this.scheduleTokenRefresh(expiresIn);

            console.log('[AuthManager] Tokens stored successfully');
            return true;
        } catch (error) {
            console.error('[AuthManager] Error storing tokens:', error);
            return false;
        }
    },

    /**
     * Get current access token
     */
    getAccessToken: function() {
        return localStorage.getItem(this.ACCESS_TOKEN_KEY);
    },

    /**
     * Get current refresh token
     */
    getRefreshToken: function() {
        return localStorage.getItem(this.REFRESH_TOKEN_KEY);
    },

    /**
     * Get stored user info
     */
    getUserInfo: function() {
        try {
            const userInfoJson = localStorage.getItem(this.USER_INFO_KEY);
            return userInfoJson ? JSON.parse(userInfoJson) : null;
        } catch (error) {
            console.error('[AuthManager] Error parsing user info:', error);
            return null;
        }
    },

    /**
     * Check if user is authenticated (has valid tokens)
     */
    isAuthenticated: function() {
        const accessToken = this.getAccessToken();
        const refreshToken = this.getRefreshToken();
        const refreshExpiry = localStorage.getItem(this.REFRESH_EXPIRY_KEY);

        if (!accessToken || !refreshToken || !refreshExpiry) {
            return false;
        }

        // Check if refresh token is expired
        const expiryDate = new Date(refreshExpiry);
        if (expiryDate <= new Date()) {
            console.log('[AuthManager] Refresh token expired');
            this.clearTokens();
            return false;
        }

        return true;
    },

    /**
     * Clear all authentication data
     */
    clearTokens: function() {
        localStorage.removeItem(this.ACCESS_TOKEN_KEY);
        localStorage.removeItem(this.REFRESH_TOKEN_KEY);
        localStorage.removeItem(this.REFRESH_EXPIRY_KEY);
        localStorage.removeItem(this.USER_INFO_KEY);

        // Also clear Blazor-specific user info keys
        localStorage.removeItem('userEmail');
        localStorage.removeItem('userFullName');
        localStorage.removeItem('userRole');
        localStorage.removeItem('userQuarryId');

        if (this.refreshTimer) {
            clearTimeout(this.refreshTimer);
            this.refreshTimer = null;
        }

        console.log('[AuthManager] Tokens cleared');
    },

    /**
     * Login with email and password
     */
    login: async function(email, password, rememberMe = true) {
        try {
            const response = await fetch('/api/auth/login', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    email: email,
                    password: password,
                    rememberMe: rememberMe
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.message || 'Login failed');
            }

            const data = await response.json();

            // Store tokens
            this.storeTokens(
                data.accessToken,
                data.refreshToken,
                data.refreshTokenExpiry,
                data.expiresIn,
                data.user
            );

            console.log('[AuthManager] Login successful');
            return { success: true, user: data.user };
        } catch (error) {
            console.error('[AuthManager] Login error:', error);
            return { success: false, error: error.message };
        }
    },

    /**
     * Logout and revoke refresh token
     */
    logout: async function() {
        const refreshToken = this.getRefreshToken();

        try {
            const accessToken = this.getAccessToken();

            if (accessToken) {
                await fetch('/api/auth/logout', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${accessToken}`
                    },
                    body: JSON.stringify({
                        refreshToken: refreshToken
                    })
                });
            }
        } catch (error) {
            console.error('[AuthManager] Logout error:', error);
        } finally {
            // Clear tokens regardless of API call success
            this.clearTokens();
            console.log('[AuthManager] Logout complete');
        }
    },

    /**
     * Refresh access token using refresh token
     */
    refreshAccessToken: async function() {
        const refreshToken = this.getRefreshToken();

        if (!refreshToken) {
            console.error('[AuthManager] No refresh token available');
            this.clearTokens();
            return false;
        }

        try {
            // Use jwt-auth endpoint to match Blazor's TokenRefreshService
            const response = await fetch('/api/jwt-auth/refresh', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    refreshToken: refreshToken
                })
            });

            if (!response.ok) {
                console.error('[AuthManager] Token refresh failed');
                this.clearTokens();

                // Redirect to login
                if (window.location.pathname !== '/Account/Login') {
                    window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                }

                return false;
            }

            const data = await response.json();

            // Store new tokens (keep existing user info)
            // Note: API uses 'Token' not 'accessToken'
            const userInfo = this.getUserInfo();
            this.storeTokens(
                data.Token || data.token || data.accessToken,
                data.RefreshToken || data.refreshToken,
                data.RefreshTokenExpiry || data.refreshTokenExpiry,
                data.ExpiresIn || data.expiresIn,
                userInfo
            );

            console.log('[AuthManager] Token refreshed successfully');
            return true;
        } catch (error) {
            console.error('[AuthManager] Token refresh error:', error);
            this.clearTokens();
            return false;
        }
    },

    /**
     * Schedule automatic token refresh before expiry
     */
    scheduleTokenRefresh: function(expiresInSeconds) {
        // Clear existing timer
        if (this.refreshTimer) {
            clearTimeout(this.refreshTimer);
        }

        // Calculate when to refresh (5 minutes before expiry)
        const expiresInMs = expiresInSeconds * 1000;
        const refreshInMs = expiresInMs - this.REFRESH_BUFFER_MS;

        // Don't schedule if already expired or too soon
        if (refreshInMs <= 0) {
            console.log('[AuthManager] Token expiring soon, refreshing immediately');
            this.refreshAccessToken();
            return;
        }

        console.log(`[AuthManager] Token refresh scheduled in ${Math.round(refreshInMs / 1000 / 60)} minutes`);

        this.refreshTimer = setTimeout(() => {
            console.log('[AuthManager] Automatic token refresh triggered');
            this.refreshAccessToken();
        }, refreshInMs);
    },

    /**
     * Get authorization header for API requests
     */
    getAuthHeader: function() {
        const token = this.getAccessToken();
        return token ? { 'Authorization': `Bearer ${token}` } : {};
    },

    /**
     * Make authenticated API request with automatic token refresh
     */
    fetchWithAuth: async function(url, options = {}) {
        // Add authorization header
        const headers = {
            ...options.headers,
            ...this.getAuthHeader()
        };

        let response = await fetch(url, { ...options, headers });

        // If unauthorized, try to refresh token and retry
        if (response.status === 401) {
            console.log('[AuthManager] Unauthorized, attempting token refresh');
            const refreshed = await this.refreshAccessToken();

            if (refreshed) {
                // Retry request with new token
                const newHeaders = {
                    ...options.headers,
                    ...this.getAuthHeader()
                };
                response = await fetch(url, { ...options, headers: newHeaders });
            } else {
                // Redirect to login if refresh failed
                if (window.location.pathname !== '/Account/Login') {
                    window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(window.location.pathname);
                }
            }
        }

        return response;
    },

    /**
     * Initialize auth manager on page load
     */
    initialize: function() {
        console.log('[AuthManager] Initializing...');

        // Check if authenticated
        if (this.isAuthenticated()) {
            console.log('[AuthManager] User authenticated');

            // Try to get current user info from API to sync state
            this.syncUserState();

            // Schedule token refresh based on stored token expiry
            // Calculate remaining time from access token
            const token = this.getAccessToken();
            if (token) {
                try {
                    // Decode JWT to get expiry (simple base64 decode)
                    const payload = JSON.parse(atob(token.split('.')[1]));
                    const expiresAt = new Date(payload.exp * 1000);
                    const now = new Date();
                    const remainingSeconds = Math.floor((expiresAt - now) / 1000);

                    if (remainingSeconds > 0) {
                        this.scheduleTokenRefresh(remainingSeconds);
                    } else {
                        // Token already expired, refresh immediately
                        this.refreshAccessToken();
                    }
                } catch (error) {
                    console.error('[AuthManager] Error parsing token:', error);
                }
            }
        } else {
            console.log('[AuthManager] User not authenticated');
        }
    },

    /**
     * Sync user state from server
     */
    syncUserState: async function() {
        try {
            const response = await this.fetchWithAuth('/api/auth/me');

            if (response.ok) {
                // Check if response is JSON before parsing
                const contentType = response.headers.get('content-type');
                if (contentType && contentType.includes('application/json')) {
                    const userInfo = await response.json();
                    localStorage.setItem(this.USER_INFO_KEY, JSON.stringify(userInfo));
                    console.log('[AuthManager] User state synced');
                } else {
                    console.warn('[AuthManager] Non-JSON response from /api/auth/me');
                }
            } else if (response.status === 401) {
                // Token is invalid, clear tokens
                console.log('[AuthManager] Token invalid, clearing auth state');
                this.clearTokens();
            }
        } catch (error) {
            // Silently handle errors - user state sync is optional
            console.log('[AuthManager] Could not sync user state (this is normal on initial load)');
        }
    }
};

// Auto-initialize on page load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        window.AuthManager.initialize();
    });
} else {
    window.AuthManager.initialize();
}
