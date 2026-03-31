using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AuctionHub.Infrastructure.Filters;

public class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAdminService adminService)
    {
        // Bypass for static files
        if (context.Request.Path.StartsWithSegments("/lib") || 
            context.Request.Path.StartsWithSegments("/css") || 
            context.Request.Path.StartsWithSegments("/js") ||
            context.Request.Path.StartsWithSegments("/images"))
        {
            await _next(context);
            return;
        }

        // Check if maintenance mode is enabled
        if (await adminService.IsMaintenanceModeEnabledAsync())
        {
            // Allow Admins to bypass maintenance mode
            if (!context.User.IsInRole("Administrator"))
            {
                // Bypass for Logout to allow users to sign out during maintenance
                if (context.Request.Path.StartsWithSegments("/Identity/Account/Logout") ||
                    context.Request.Path.StartsWithSegments("/Identity/Account/Login") ||
                    context.Request.Path.StartsWithSegments("/signin-google") ||
                    context.Request.Path.StartsWithSegments("/signin-github") ||
                    context.Request.Path.StartsWithSegments("/Identity/Account/ExternalLogin"))
                {
                    await _next(context);
                    return;
                }

                // If not already on the maintenance page, redirect
                if (!context.Request.Path.StartsWithSegments("/Home/Maintenance"))
                {
                    context.Response.Redirect("/Home/Maintenance");
                    return;
                }
            }
        }

        await _next(context);
    }
}
