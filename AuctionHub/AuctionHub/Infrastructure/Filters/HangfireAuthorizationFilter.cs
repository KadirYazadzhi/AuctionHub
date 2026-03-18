using Hangfire.Dashboard;

namespace AuctionHub.Infrastructure.Filters;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Allow in development, restrict in production if needed
        return true; 
    }
}
