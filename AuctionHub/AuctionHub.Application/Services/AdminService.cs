using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AuctionHub.Application.Services;

public class AdminService : IAdminService
{
    private readonly IAuctionHubDbContext _context;
    private readonly IDistributedCache _cache;

    public AdminService(IAuctionHubDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
    {
        var now = DateTime.UtcNow;
        var stats = new AdminDashboardStatsDto();

        // 1. Total Revenue (Promotion fees + Commissions)
        // Note: AdminRefunds are typically returns of escrow or user-to-user funds, 
        // they shouldn't be deducted from the site's EARNED revenue (fees/commissions).
        var revenueTypes = new[] { "Promotion", "Commission" };
        stats.TotalRevenue = await _context.Transactions
            .Where(t => revenueTypes.Contains(t.TransactionType))
            .SumAsync(t => t.Amount);

        stats.DailyRevenue = await _context.Transactions
            .Where(t => revenueTypes.Contains(t.TransactionType) && t.TransactionDate >= now.Date)
            .SumAsync(t => t.Amount);

        // 2. Active Escrow
        stats.ActiveEscrowAmount = await _context.Transactions
            .Where(t => t.TransactionType == "Escrow")
            .SumAsync(t => t.Amount);

        // 3. General Stats
        stats.ActiveUsersCount = await _context.Users.CountAsync();
        stats.NewUsersToday = await _context.Users.CountAsync(u => u.CreatedOn >= now.Date);
        stats.TotalAuctionsCount = await _context.Auctions.CountAsync();

        // 4. Top Categories by Bid Count
        stats.TopCategories = await _context.Bids
            .GroupBy(b => b.Auction.Category.Name)
            .Select(g => new CategoryStatDto
            {
                Name = g.Key,
                BidCount = g.Count()
            })
            .OrderByDescending(c => c.BidCount)
            .Take(5)
            .ToListAsync();

        // 5. Activity Trend (last 7 days)
        var sevenDaysAgo = now.Date.AddDays(-7);
        stats.ActivityTrend = await _context.Bids
            .Where(b => b.BidTime >= sevenDaysAgo)
            .GroupBy(b => b.BidTime.Date)
            .Select(g => new DailyActivityDto
            {
                Date = g.Key,
                BidCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        return stats;
    }

    public async Task<IEnumerable<SystemSetting>> GetSystemSettingsAsync()
    {
        return await _context.SystemSettings.ToListAsync();
    }

    public async Task<bool> UpdateSystemSettingAsync(string key, string value, string adminId)
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null)
        {
            setting = new SystemSetting { Key = key };
            _context.SystemSettings.Add(setting);
        }

        string oldDetails = $"Old Value: {setting.Value}";
        setting.Value = value;
        setting.LastUpdated = DateTime.UtcNow;

        await LogActionAsync(adminId, "Update System Setting", "SystemSetting", key, $"{oldDetails} -> New Value: {value}");
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaginatedList<AuditLog>> GetAuditLogsAsync(int pageNumber, int pageSize)
    {
        var query = _context.AuditLogs
            .Include(l => l.Admin)
            .OrderByDescending(l => l.Timestamp);

        return await PaginatedList<AuditLog>.CreateAsync(query, pageNumber, pageSize);
    }

    public async Task LogActionAsync(string adminId, string action, string entityName, string entityId, string details)
    {
        var log = new AuditLog
        {
            AdminId = adminId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<PaginatedList<UserReport>> GetUserReportsAsync(int pageNumber, int pageSize, bool includeResolved)
    {
        var query = _context.UserReports
            .Include(r => r.Reporter)
            .Include(r => r.ReportedUser)
            .Include(r => r.ReportedAuction)
            .Where(r => includeResolved || !r.IsResolved)
            .OrderByDescending(r => r.CreatedOn)
            .AsQueryable();

        return await PaginatedList<UserReport>.CreateAsync(query, pageNumber, pageSize);
    }

    public async Task<bool> ResolveReportAsync(int reportId, string adminNotes, string adminId)
    {
        var report = await _context.UserReports.FindAsync(reportId);
        if (report == null) return false;

        report.IsResolved = true;
        report.AdminNotes = adminNotes;

        await LogActionAsync(adminId, "Resolved User Report", "UserReport", reportId.ToString(), adminNotes);
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<SuspiciousActivityDto>> GetSuspiciousActivitiesAsync()
    {
        var activities = new List<SuspiciousActivityDto>();
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);

        // 1. Rapid Price Increase (> 500% in 1 hour)
        var fastGrowingAuctions = await _context.Bids
            .Where(b => b.BidTime >= oneHourAgo)
            .GroupBy(b => b.AuctionId)
            .Select(g => new { 
                AuctionId = g.Key, 
                Increase = g.Max(b => b.Amount) - g.Min(b => b.Amount),
                StartAmount = g.Min(b => b.Amount)
            })
            .Where(x => x.StartAmount > 0 && (x.Increase / x.StartAmount) > 5)
            .ToListAsync();

        foreach (var a in fastGrowingAuctions)
        {
            activities.Add(new SuspiciousActivityDto {
                Type = "Rapid Price Growth",
                Description = $"Price increased by over 500% in the last hour.",
                Severity = "Medium",
                AuctionId = a.AuctionId,
                DetectedOn = now
            });
        }

        // 2. High Value Bids from New Users (Balance > 1000 and joined < 24h)
        var newUsersHighBids = await _context.Users
            .Where(u => u.CreatedOn >= now.AddDays(-1) && u.WalletBalance > 1000)
            .Select(u => new { u.Id, u.UserName, u.WalletBalance })
            .ToListAsync();

        foreach (var u in newUsersHighBids)
        {
            activities.Add(new SuspiciousActivityDto {
                Type = "High Value New User",
                Description = $"User {u.UserName} has a balance of {u.WalletBalance:C} within 24h of joining.",
                Severity = "Low",
                UserId = u.Id,
                DetectedOn = now
            });
        }

        return activities;
    }

    public async Task<IEnumerable<AuctionDto>> GetDisputedAuctionsAsync()
    {
        return await _context.Auctions
            .Include(a => a.Category)
            .Include(a => a.Seller)
            .Include(a => a.Bids)
            .Where(a => a.IsDisputed && !a.IsSettled)
            .Select(a => new AuctionDto
            {
                Id = a.Id,
                Title = a.Title,
                ImageUrl = a.ImageUrl,
                CurrentPrice = a.CurrentPrice,
                EndTime = a.EndTime,
                Category = a.Category.Name,
                IsActive = a.IsActive,
                IsPromoted = a.IsPromoted,
                IsSuspended = a.IsSuspended,
                SellerId = a.SellerId,
                SellerName = a.Seller.UserName ?? a.Seller.Email ?? "Unknown",
                IsWinning = false // Not used in this context
            })
            .ToListAsync();
    }

    public async Task<bool> ResolveDisputeAsync(int auctionId, string resolution, string adminId)
    {
        var auction = await _context.Auctions
            .Include(a => a.Seller)
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Id == auctionId);

        if (auction == null || !auction.IsDisputed || auction.IsSettled) return false;

        var winningBid = auction.Bids.OrderByDescending(b => b.Amount).FirstOrDefault();
        if (winningBid == null) return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            if (resolution == "Refund")
            {
                // Refund the Buyer (Winner)
                var winner = await _context.Users.FindAsync(winningBid.BidderId);
                if (winner != null)
                {
                    winner.WalletBalance += winningBid.Amount;
                    _context.Transactions.Add(new Transaction
                    {
                        UserId = winner.Id,
                        Amount = winningBid.Amount,
                        Description = $"Admin Refund for disputed auction '{auction.Title}'",
                        TransactionType = "AdminRefund",
                        TransactionDate = DateTime.UtcNow,
                        AuctionId = auctionId
                    });
                }
            }
            else if (resolution == "Release")
            {
                // Release Funds to Seller
                auction.Seller.WalletBalance += winningBid.Amount;
                _context.Transactions.Add(new Transaction
                {
                    UserId = auction.SellerId,
                    Amount = winningBid.Amount,
                    Description = $"Admin Release for disputed auction '{auction.Title}'",
                    TransactionType = "Sale",
                    TransactionDate = DateTime.UtcNow,
                    AuctionId = auctionId
                });
            }
            else
            {
                return false;
            }

            auction.IsSettled = true;
            auction.IsDisputed = false;

            await LogActionAsync(adminId, "Resolve Auction Dispute", "Auction", auctionId.ToString(), $"Resolution: {resolution}");
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<byte[]> ExportTransactionsToCsvAsync()
    {
        var transactions = await _context.Transactions
            .Include(t => t.User)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var builder = new System.Text.StringBuilder();
        // CSV Header
        builder.AppendLine("Date,User,Type,Amount,AuctionId,Description");

        foreach (var t in transactions)
        {
            builder.AppendLine($"{t.TransactionDate:yyyy-MM-dd HH:mm:ss}," +
                               $"\"{t.User?.UserName ?? "Deleted User"}\"," +
                               $"\"{t.TransactionType}\"," +
                               $"{t.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}," +
                               $"{t.AuctionId?.ToString() ?? "N/A"}," +
                               $"\"{t.Description.Replace("\"", "'")}\"");
        }

        return System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public async Task ClearCacheAsync()
    {
        // Invalidate known cache keys
        await _cache.RemoveAsync("Categories");
        await _cache.RemoveAsync("ending_soon_4_anonymous");
        await _cache.RemoveAsync("ending_soon_8_anonymous");
    }

    public async Task<(bool Enabled, string Message)> ToggleMaintenanceModeAsync()
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "MaintenanceMode");
        if (setting == null)
        {
            setting = new SystemSetting 
            { 
                Key = "MaintenanceMode", 
                Value = "false",
                Description = "Disables public access to the site when set to true."
            };
            _context.SystemSettings.Add(setting);
        }

        bool currentlyEnabled = setting.Value.ToLower() == "true";
        bool newState = !currentlyEnabled;
        setting.Value = newState.ToString().ToLower();
        setting.LastUpdated = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _cache.RemoveAsync("MaintenanceMode"); // Force refresh on next check

        return (newState, newState ? "Maintenance mode activated." : "Maintenance mode deactivated.");
    }

    public async Task<bool> IsMaintenanceModeEnabledAsync()
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "MaintenanceMode");
        return setting?.Value.ToLower() == "true";
    }
}
