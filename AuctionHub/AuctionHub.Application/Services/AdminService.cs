using AuctionHub.Application.Interfaces;
using AuctionHub.Application.DTOs;
using AuctionHub.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Application.Services;

public class AdminService : IAdminService
{
    private readonly IAuctionHubDbContext _context;

    public AdminService(IAuctionHubDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardStatsDto> GetDashboardStatsAsync()
    {
        var now = DateTime.UtcNow;
        var stats = new AdminDashboardStatsDto();

        // 1. Total Revenue (Promotion fees)
        stats.TotalRevenue = await _context.Transactions
            .Where(t => t.TransactionType == "Promotion")
            .SumAsync(t => t.Amount);

        // 2. Active Escrow (Purchases that haven't reached seller yet)
        // We identify Escrow by finding 'Purchase' or 'Escrow' transactions 
        // that don't have a matching 'Sale' transaction yet.
        // Simplified: Sum of all transactions type 'Escrow'
        stats.ActiveEscrowAmount = await _context.Transactions
            .Where(t => t.TransactionType == "Escrow")
            .SumAsync(t => t.Amount);

        // 3. General Stats
        stats.ActiveUsersCount = await _context.Users.CountAsync();
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
}
