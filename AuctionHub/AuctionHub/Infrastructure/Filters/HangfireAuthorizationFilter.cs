using Hangfire.Dashboard;

namespace AuctionHub.Infrastructure.Filters;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Strictly allow only authenticated Administrators
        return httpContext.User.Identity?.IsAuthenticated == true && 
               httpContext.User.IsInRole("Administrator");
    }
}
