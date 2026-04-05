using Microsoft.EntityFrameworkCore;
using Polly;
using AuctionHub.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace AuctionHub.Infrastructure.Extensions;

public static class DatabaseExtensions
{
    public static void ApplyMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuctionHubDbContext>();

        // Define a retry policy: 5 retries with exponential backoff (2, 4, 8, 16, 32 seconds)
        var retryPolicy = Policy
            .Handle<SqlException>()
            .WaitAndRetry(
                retryCount: 5,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retry, context) =>
                {
                    Console.WriteLine($"[Database Migration] Attempt {retry} failed: {exception.Message}. Retrying in {timeSpan.TotalSeconds} seconds...");
                });

        try
        {
            retryPolicy.Execute(() =>
            {
                if (dbContext.Database.GetPendingMigrations().Any())
                {
                    Console.WriteLine("[Database Migration] Applying pending migrations...");
                    dbContext.Database.Migrate();
                    Console.WriteLine("[Database Migration] Migrations applied successfully.");
                }
                else
                {
                    Console.WriteLine("[Database Migration] No pending migrations found.");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Database Migration] Critical error after multiple retries: {ex.Message}");
            throw;
        }
    }
}
