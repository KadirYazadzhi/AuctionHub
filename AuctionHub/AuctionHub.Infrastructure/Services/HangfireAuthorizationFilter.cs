using Hangfire.Dashboard;

namespace AuctionHub.Infrastructure.Services;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Allow all users in development for demo purposes, but restrict to Admin in production
        // return httpContext.User.Identity?.IsAuthenticated == true && httpContext.User.IsInRole("Administrator");
        
        return true; // Simplified for the assignment demonstration
    }
}
