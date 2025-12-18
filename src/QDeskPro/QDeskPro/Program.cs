using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;
using QDeskPro.Client.Pages;
using QDeskPro.Components;
using QDeskPro.Components.Account;
using QDeskPro.Data;
using QDeskPro.Data.Seed;
using QDeskPro.Domain.Entities;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File("logs/qdeskpro-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting QDeskPro application");
    
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

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

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents()
        .AddInteractiveWebAssemblyComponents()
        .AddAuthenticationStateSerialization();

    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<IdentityRedirectManager>();
    builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();

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
    
    // Use SQLite in development, SQL Server in production
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));
    }
    else
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));
    }
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false; // For development, set to true in production
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

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
    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }
    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.UseAntiforgery();

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode()
        .AddInteractiveWebAssemblyRenderMode()
        .AddAdditionalAssemblies(typeof(QDeskPro.Client._Imports).Assembly);

    // Add additional endpoints required by the Identity /Account Razor components.
    app.MapAdditionalIdentityEndpoints();

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
