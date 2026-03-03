using AuctionHub.Application.DTOs;
using AuctionHub.Domain.Models;

namespace AuctionHub.Application.Interfaces;

public interface IAdminService
{
    // Dashboard Stats
    Task<AdminDashboardStatsDto> GetDashboardStatsAsync();
    
    // System Settings
    Task<IEnumerable<SystemSetting>> GetSystemSettingsAsync();
    Task<bool> UpdateSystemSettingAsync(string key, string value, string adminId);
    
    // Audit Logs
    Task<PaginatedList<AuditLog>> GetAuditLogsAsync(int pageNumber, int pageSize);
    Task LogActionAsync(string adminId, string action, string entityName, string entityId, string details);
    
    // User Reports
    Task<PaginatedList<UserReport>> GetUserReportsAsync(int pageNumber, int pageSize, bool includeResolved);
    Task<bool> ResolveReportAsync(int reportId, string adminNotes, string adminId);
    
    // Fraud Detection
    Task<List<SuspiciousActivityDto>> GetSuspiciousActivitiesAsync();

    // Disputes
    Task<IEnumerable<AuctionDto>> GetDisputedAuctionsAsync();
    Task<bool> ResolveDisputeAsync(int auctionId, string resolution, string adminId);

    // Export
    Task<byte[]> ExportTransactionsToCsvAsync();
}

public class SuspiciousActivityDto
{
    public string Type { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Severity { get; set; } = "Low"; // Low, Medium, High
    public int? AuctionId { get; set; }
    public string? UserId { get; set; }
    public DateTime DetectedOn { get; set; }
}

public class AdminDashboardStatsDto
{
    public decimal TotalRevenue { get; set; }
    public decimal DailyRevenue { get; set; }
    public decimal ActiveEscrowAmount { get; set; }
    public int ActiveUsersCount { get; set; }
    public int NewUsersToday { get; set; }
    public int TotalAuctionsCount { get; set; }
    public List<CategoryStatDto> TopCategories { get; set; } = new();
    public List<DailyActivityDto> ActivityTrend { get; set; } = new();
}
