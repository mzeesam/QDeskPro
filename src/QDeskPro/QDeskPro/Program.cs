using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using QDeskPro.Components;
using QDeskPro.Components.Account;
using QDeskPro.Data;
using QDeskPro.Data.Seed;
using QDeskPro.Domain.Entities;
using QDeskPro.Api;
using QDeskPro.Shared.Middleware;
using Serilog;
using Serilog.Events;
using QDeskPro.Domain.Models.AI;
using QDeskPro.Domain.Services.AI;
using System.Threading.RateLimiting;

// Configure Serilog with structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "QDeskPro")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/qdeskpro-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    // Security audit log - separate file for security events (login, logout, failed attempts, etc.)
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e =>
            e.Properties.ContainsKey("SecurityEvent") ||
            e.MessageTemplate.Text.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            e.MessageTemplate.Text.Contains("logout", StringComparison.OrdinalIgnoreCase) ||
            e.MessageTemplate.Text.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            e.MessageTemplate.Text.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            e.MessageTemplate.Text.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
            e.MessageTemplate.Text.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
            e.MessageTemplate.Text.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        .WriteTo.File(
            path: "logs/security-audit-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 90,  // Keep security logs longer
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"))
    .CreateLogger();

try
{
    Log.Information("Starting QDeskPro application");
    
    var builder = WebApplication.CreateBuilder(args);

    // Add Aspire service defaults (OpenTelemetry, health checks, resilience)
    builder.AddServiceDefaults();

    builder.Host.UseSerilog();

    // Security: Configure forwarded headers for reverse proxy scenarios (nginx, Azure, etc.)
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // Security: Rate Limiting to prevent abuse
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Global rate limit - 100 requests per minute per user/IP
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Authentication endpoints - stricter limit to prevent brute force
        options.AddFixedWindowLimiter("auth", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 2;
        });

        // API endpoints
        options.AddFixedWindowLimiter("api", limiterOptions =>
        {
            limiterOptions.PermitLimit = 50;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 5;
        });
    });

    // Security: Configure Kestrel with security options
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.AddServerHeader = false; // Remove server header to reduce fingerprinting
        options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB limit
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
        options.Limits.MaxConcurrentConnections = 1000;
        options.Limits.MaxConcurrentUpgradedConnections = 100;
    });

    // Add MudBlazor services
    builder.Services.AddMudServices(config =>
    {
        config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
        config.SnackbarConfiguration.PreventDuplicates = false;
        config.SnackbarConfiguration.NewestOnTop = false;
        config.SnackbarConfiguration.ShowCloseIcon = true;
        config.SnackbarConfiguration.VisibleStateDuration = 5000;
        config.SnackbarConfiguration.HideTransitionDuration = 300;
        config.SnackbarConfiguration.ShowTransitionDuration = 300;
    });

    // Add HttpContextAccessor for SSR pages to access HttpContext
    builder.Services.AddHttpContextAccessor();

    // Add services to the container - Pure Blazor Server mode
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<IdentityRedirectManager>();

    // Configure JWT settings
    builder.Services.Configure<QDeskPro.Shared.Models.JwtSettings>(
        builder.Configuration.GetSection("JwtSettings"));

    // Register HttpClient factory (required for CustomAuthenticationStateProvider)
    builder.Services.AddHttpClient();

    // Register JWT and authentication services
    builder.Services.AddScoped<QDeskPro.Domain.Services.JwtTokenService>();
    builder.Services.AddScoped<QDeskPro.Shared.Services.LocalStorageService>();
    builder.Services.AddScoped<AuthenticationStateProvider, QDeskPro.Shared.Services.CustomAuthenticationStateProvider>();

    // Register HttpClient for API calls from interactive components
    builder.Services.AddScoped(sp =>
    {
        var navigationManager = sp.GetRequiredService<NavigationManager>();
        return new HttpClient
        {
            BaseAddress = new Uri(navigationManager.BaseUri)
        };
    });

    var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    });

    authBuilder.AddIdentityCookies(options =>
    {
        // Configure persistent cookies for "Remember Me" functionality
        options.ApplicationCookie?.Configure(cookieOptions =>
        {
            // Security: Cookie naming to avoid conflicts
            cookieOptions.Cookie.Name = ".QDeskPro.Auth";

            // Security: HttpOnly prevents JavaScript access (XSS protection)
            cookieOptions.Cookie.HttpOnly = true;

            // Security: Secure policy - Always in production, SameAsRequest for dev flexibility
            cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            // Security: SameSite prevents CSRF attacks
            // Lax allows GET requests from external sites (for OAuth redirects)
            // Strict would be more secure but may break OAuth flows
            cookieOptions.Cookie.SameSite = SameSiteMode.Lax;

            // Security: Cookie expiration (30 days for "Remember Me")
            cookieOptions.ExpireTimeSpan = TimeSpan.FromDays(30);
            cookieOptions.SlidingExpiration = true; // Renew on activity

            // Paths
            cookieOptions.LoginPath = "/Account/Login";
            cookieOptions.LogoutPath = "/Account/Logout";
            cookieOptions.AccessDeniedPath = "/Account/AccessDenied";

            // Security: Events for audit logging
            cookieOptions.Events.OnSignedIn = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("User {User} signed in from {IP}",
                    context.Principal?.Identity?.Name,
                    context.HttpContext.Connection.RemoteIpAddress);
                return Task.CompletedTask;
            };
            cookieOptions.Events.OnSigningOut = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("User {User} signed out from {IP}",
                    context.HttpContext.User?.Identity?.Name,
                    context.HttpContext.Connection.RemoteIpAddress);
                return Task.CompletedTask;
            };
            cookieOptions.Events.OnValidatePrincipal = context =>
            {
                // Security: Log failed authentication attempts
                if (context.Principal == null)
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Authentication validation failed from {IP}",
                        context.HttpContext.Connection.RemoteIpAddress);
                }
                return Task.CompletedTask;
            };
        });

        // Security: External cookie configuration
        options.ExternalCookie?.Configure(externalOptions =>
        {
            externalOptions.Cookie.Name = ".QDeskPro.External";
            externalOptions.Cookie.HttpOnly = true;
            externalOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            externalOptions.Cookie.SameSite = SameSiteMode.Lax;
            externalOptions.ExpireTimeSpan = TimeSpan.FromMinutes(10); // Short-lived
        });

        // Security: Two-factor cookie configuration
        options.TwoFactorRememberMeCookie?.Configure(tfaOptions =>
        {
            tfaOptions.Cookie.Name = ".QDeskPro.2FA";
            tfaOptions.Cookie.HttpOnly = true;
            tfaOptions.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            tfaOptions.Cookie.SameSite = SameSiteMode.Strict;
            tfaOptions.ExpireTimeSpan = TimeSpan.FromDays(14); // 2 weeks
        });
    });

    authBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Configure JWT Bearer events for logging
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT authentication failed: {Error} from {IP}",
                    context.Exception.Message,
                    context.HttpContext.Connection.RemoteIpAddress);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogDebug("JWT token validated for user {User} from {IP}",
                    context.Principal?.Identity?.Name,
                    context.HttpContext.Connection.RemoteIpAddress);
                return Task.CompletedTask;
            }
        };
    });

    // Configure authorization policies
    builder.Services.AddAuthorization(options =>
    {
        // Administrator-only actions
        options.AddPolicy("RequireAdministrator", policy =>
            policy.RequireRole("Administrator"));

        // Manager or Administrator
        options.AddPolicy("RequireManagerOrAdmin", policy =>
            policy.RequireRole("Administrator", "Manager"));

        // Clerk operations
        options.AddPolicy("RequireClerk", policy =>
            policy.RequireRole("Clerk"));

        // Any authenticated user
        options.AddPolicy("RequireAuthenticated", policy =>
            policy.RequireAuthenticatedUser());

        // Custom policy for quarry access
        options.AddPolicy("RequireQuarryAccess", policy =>
            policy.Requirements.Add(new QDeskPro.Shared.Authorization.QuarryAccessRequirement()));
    });

    // Register QuarryAccessHandler
    builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, QDeskPro.Shared.Authorization.QuarryAccessHandler>();

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    // Use SQL Server for all environments
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null);
            sqlOptions.CommandTimeout(30);
        });
    });

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Sign-in settings
            options.SignIn.RequireConfirmedAccount = false; // For development, set to true in production
            options.SignIn.RequireConfirmedEmail = false;   // Set to true in production

            // Security: Strong password requirements
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 12;        // Increased from 8 to 12
            options.Password.RequiredUniqueChars = 6;    // Require 6 unique characters

            // Security: Account lockout settings
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User settings
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

    // Security: Configure token lifespan for password reset and email confirmation
    builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    {
        options.TokenLifespan = TimeSpan.FromHours(24); // Tokens expire after 24 hours
    });

    // Configure Data Protection for consistent antiforgery tokens across restarts
    // This is CRITICAL for avoiding antiforgery token validation errors
    var keysDirectory = Path.Combine(builder.Environment.ContentRootPath, "keys");
    Directory.CreateDirectory(keysDirectory);
    builder.Services.AddDataProtection()
        .SetApplicationName("QDeskPro")
        .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
        .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

    // Configure Antiforgery with explicit settings
    // Use SameAsRequest to allow both HTTP (dev) and HTTPS (prod) - the HTTPS redirection
    // middleware will upgrade HTTP to HTTPS in production anyway
    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = "QDeskPro.Antiforgery";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
        options.FormFieldName = "__RequestVerificationToken";
        options.HeaderName = "X-CSRF-TOKEN";
    });

    // Add memory cache (required by DashboardService and MasterDataService)
    builder.Services.AddMemoryCache();

    // Register application services
    builder.Services.AddScoped<QDeskPro.Domain.Services.SaleCalculationService>();
    builder.Services.AddScoped<QDeskPro.Features.Dashboard.Services.DashboardService>();
    builder.Services.AddScoped<QDeskPro.Features.Dashboard.Services.AnalyticsService>();
    builder.Services.AddScoped<QDeskPro.Features.Dashboard.Services.LiveOperationsService>();
    builder.Services.AddScoped<QDeskPro.Features.Dashboard.Services.DataAnalyticsService>();
    builder.Services.AddScoped<QDeskPro.Features.Dashboard.Services.ROIAnalysisService>();
    builder.Services.AddScoped<QDeskPro.Features.AI.Services.PredictiveAnalyticsService>();
    builder.Services.AddScoped<QDeskPro.Features.Sales.Services.SaleService>();
    builder.Services.AddScoped<QDeskPro.Features.Prepayments.Services.PrepaymentService>();
    builder.Services.AddScoped<QDeskPro.Features.Expenses.Services.ExpenseService>();
    builder.Services.AddScoped<QDeskPro.Features.Banking.Services.BankingService>();
    builder.Services.AddScoped<QDeskPro.Features.FuelUsage.Services.FuelUsageService>();
    builder.Services.AddScoped<QDeskPro.Features.Reports.Services.ReportService>();
    builder.Services.AddScoped<QDeskPro.Features.Reports.Services.ManagerReportService>();
    builder.Services.AddScoped<QDeskPro.Features.Reports.Services.ExcelExportService>();
    builder.Services.AddScoped<QDeskPro.Features.Reports.Services.ReportExportService>();
    builder.Services.AddScoped<QDeskPro.Features.MasterData.Services.MasterDataService>();
    builder.Services.AddScoped<QDeskPro.Features.MasterData.Services.DataManagementService>();
    builder.Services.AddScoped<QDeskPro.Features.Admin.Services.UserService>();

    // Register Accounting services
    builder.Services.AddScoped<QDeskPro.Features.Accounting.Services.IAccountingService, QDeskPro.Features.Accounting.Services.AccountingService>();
    builder.Services.AddScoped<QDeskPro.Features.Accounting.Services.IFinancialReportService, QDeskPro.Features.Accounting.Services.FinancialReportService>();
    builder.Services.AddScoped<QDeskPro.Features.Accounting.Services.IFinancialReportExportService, QDeskPro.Features.Accounting.Services.FinancialReportExportService>();

    // Configure email settings
    builder.Services.Configure<QDeskPro.Features.Reports.Services.EmailSettings>(
        builder.Configuration.GetSection(QDeskPro.Features.Reports.Services.EmailSettings.SectionName));

    // Register email services
    builder.Services.AddScoped<QDeskPro.Features.Reports.Services.ReportEmailService>();
    builder.Services.AddSingleton<QDeskPro.Features.Reports.Services.IEmailQueue, QDeskPro.Features.Reports.Services.EmailQueueService>();
    builder.Services.AddHostedService(provider =>
        (QDeskPro.Features.Reports.Services.EmailQueueService)provider.GetRequiredService<QDeskPro.Features.Reports.Services.IEmailQueue>());

    // Configure AI settings
    builder.Services.Configure<AIConfiguration>(
        builder.Configuration.GetSection(AIConfiguration.SectionName));

    // Register AI services
    builder.Services.AddSingleton<IAIProviderFactory, AIProviderFactory>();
    builder.Services.AddScoped<ISalesQueryService, SalesQueryService>();
    builder.Services.AddScoped<IChatCompletionService, ChatCompletionService>();
    builder.Services.AddScoped<QDeskPro.Features.AI.Services.ISalesAnalyticsService, QDeskPro.Features.AI.Services.SalesAnalyticsService>();

    // Add global exception handler
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Security: Configure HSTS with stronger settings
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromDays(365); // 1 year (increased from 30 days default)
    });

    // Add custom health checks (in addition to Aspire defaults)
    builder.Services.AddHealthChecks()
        .AddCheck<QDeskPro.Shared.HealthChecks.DatabaseHealthCheck>(
            "database",
            failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            tags: new[] { "db", "sql" });

    var app = builder.Build();

    // Seed the database
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        // Seed data
        await SeedData.SeedAsync(app.Services);
    }

    // Configure the HTTP request pipeline.

    // Security: Use forwarded headers (for reverse proxy scenarios)
    app.UseForwardedHeaders();

    // Add request logging middleware (before exception handler)
    app.UseRequestLogging();

    // Add global exception handler
    app.UseExceptionHandler();

    // Security: Add security headers middleware
    app.Use(async (context, next) =>
    {
        // Security headers to prevent common attacks
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");

        // Content Security Policy (CSP) - Critical for XSS protection
        // Allows Blazor, MudBlazor, Chart.js CDN, and SignalR to function while blocking unsafe content
        var csp = string.Join("; ",
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net",  // Required for Blazor + Chart.js CDN
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",  // Required for MudBlazor
            "font-src 'self' https://fonts.gstatic.com",
            "img-src 'self' data: https:",
            "connect-src 'self' wss: ws: https://cdn.jsdelivr.net",  // Required for SignalR WebSocket + CDN
            "frame-ancestors 'none'",
            "form-action 'self'",
            "base-uri 'self'"
            // REMOVED: "upgrade-insecure-requests" - This forces HTTPS, disabled for HTTP-only deployment
        );
        context.Response.Headers.Append("Content-Security-Policy", csp);

        // Remove server information headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");

        await next();
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        // Security: HSTS with 1-year max age, preload, and include subdomains
        // DISABLED for HTTP-only deployment - uncomment when SSL/TLS is configured
        // app.UseHsts();
    }
    // Removed: app.UseStatusCodePagesWithReExecute("/not-found");
    // This was intercepting Account page routes before Razor Components routing could handle them

    // HTTPS Redirection - Only enable when SSL/TLS is configured on the server
    // Comment out the line below if deploying to HTTP-only environments
    // app.UseHttpsRedirection();

    // Security: Enable rate limiting
    app.UseRateLimiter();

    // Authentication and Authorization must come BEFORE Antiforgery
    // This ensures antiforgery tokens are properly validated based on authenticated user state
    // See: https://github.com/dotnet/aspnetcore/issues/50760
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseAntiforgery();

    // Configure static file serving with explicit MIME types
    var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    provider.Mappings[".css"] = "text/css";
    provider.Mappings[".js"] = "application/javascript";
    provider.Mappings[".json"] = "application/json";
    provider.Mappings[".woff"] = "font/woff";
    provider.Mappings[".woff2"] = "font/woff2";
    provider.Mappings[".svg"] = "image/svg+xml";

    app.UseStaticFiles(new StaticFileOptions
    {
        ContentTypeProvider = provider,
        OnPrepareResponse = ctx =>
        {
            // Add cache headers for static files
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=31536000");
        }
    });

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Add additional endpoints required by the Identity /Account Razor components.
    app.MapAdditionalIdentityEndpoints();

    // Map API endpoints (rate limiting applied at endpoint group level)
    app.MapApiEndpoints();

    // Map Aspire default endpoints (health checks: /health, /alive)
    app.MapDefaultEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
