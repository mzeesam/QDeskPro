using QDeskPro.Api.Endpoints;

namespace QDeskPro.Api;

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        // Map all API endpoints
        app.MapAuthEndpoints();
        app.MapJwtAuthEndpoints(); // JWT authentication endpoints
        app.MapSalesEndpoints();
        app.MapExpenseEndpoints();
        app.MapBankingEndpoints();
        app.MapFuelUsageEndpoints();
        app.MapReportEndpoints();
        app.MapMasterDataEndpoints();
        app.MapDashboardEndpoints();
        app.MapAIEndpoints();

        return app;
    }
}
