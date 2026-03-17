using AuctionHub.Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AuctionHub.Application.Interfaces;

public interface IAuctionHubDbContext
{
    DbSet<Auction> Auctions { get; set; }
    DbSet<Category> Categories { get; set; }
    DbSet<Bid> Bids { get; set; }
    DbSet<Transaction> Transactions { get; set; }
    DbSet<AuctionWatchlist> Watchlist { get; set; }
    DbSet<Notification> Notifications { get; set; }
    DbSet<ContactMessage> ContactMessages { get; set; }
    DbSet<ChatMessage> ChatMessages { get; set; }
    DbSet<AutoBid> AutoBids { get; set; }
    DbSet<Review> Reviews { get; set; }
    DbSet<AuditLog> AuditLogs { get; set; }
    DbSet<SystemSetting> SystemSettings { get; set; }
    DbSet<UserReport> UserReports { get; set; }
    DbSet<PrivateOffer> PrivateOffers { get; set; }
    DbSet<AuctionParticipant> AuctionParticipants { get; set; }
    DbSet<UserFollower> UserFollowers { get; set; }
    DbSet<Comment> Comments { get; set; }
    DbSet<ApplicationUser> Users { get; set; }
    DbSet<IdentityRole> Roles { get; set; }
    DbSet<IdentityUserRole<string>> UserRoles { get; set; }

    DatabaseFacade Database { get; }

    EntityEntry Entry(object entity);
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry Update(object entity);
    EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class;
    EntityEntry Add(object entity);
    EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
