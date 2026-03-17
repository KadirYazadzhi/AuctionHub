using AuctionHub.Domain.Models;
using AuctionHub.Application.Interfaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Infrastructure.Data;

public class AuctionHubDbContext : IdentityDbContext<ApplicationUser>, IAuctionHubDbContext
{
    public AuctionHubDbContext(DbContextOptions<AuctionHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<Auction> Auctions { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Bid> Bids { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<AuctionWatchlist> Watchlist { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<ContactMessage> ContactMessages { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public DbSet<AutoBid> AutoBids { get; set; } = null!;
    public DbSet<AuctionImage> AuctionImages { get; set; } = null!;
    public DbSet<Review> Reviews { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;
    public DbSet<UserReport> UserReports { get; set; } = null!;
    public DbSet<PrivateOffer> PrivateOffers { get; set; } = null!;
    public DbSet<AuctionParticipant> AuctionParticipants { get; set; } = null!;
    public DbSet<UserFollower> UserFollowers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Social: User Followers ---
        builder.Entity<UserFollower>()
            .HasKey(f => new { f.FollowerId, f.SellerId });

        builder.Entity<UserFollower>()
            .HasOne(f => f.Follower)
            .WithMany(u => u.Following)
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserFollower>()
            .HasOne(f => f.Seller)
            .WithMany(u => u.Followers)
            .HasForeignKey(f => f.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Review
        builder.Entity<Review>()
            .HasOne(r => r.Auction)
            .WithMany()
            .HasForeignKey(r => r.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.Reviewer)
            .WithMany()
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Review>()
            .HasOne(r => r.TargetUser)
            .WithMany(u => u.ReceivedReviews)
            .HasForeignKey(r => r.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure AutoBid
        builder.Entity<AutoBid>()
            .HasOne(ab => ab.Auction)
            .WithMany()
            .HasForeignKey(ab => ab.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AutoBid>()
            .HasOne(ab => ab.User)
            .WithMany()
            .HasForeignKey(ab => ab.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure relations and delete behaviors if necessary
        builder.Entity<Auction>()
            .HasOne(a => a.Seller)
            .WithMany(u => u.MyAuctions)
            .HasForeignKey(a => a.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Bid>()
            .HasOne(b => b.Bidder)
            .WithMany(u => u.MyBids)
            .HasForeignKey(b => b.BidderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Bid>()
            .HasOne(b => b.Auction)
            .WithMany(a => a.Bids)
            .HasForeignKey(b => b.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChatMessage configurations to avoid multiple cascade paths
        builder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Receiver)
            .WithMany()
            .HasForeignKey(m => m.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Auction)
            .WithMany()
            .HasForeignKey(m => m.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Precision for money
        builder.Entity<Auction>().Property(a => a.CurrentPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Auction>().Property(a => a.StartPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Auction>().Property(a => a.MinIncrease).HasColumnType("decimal(18,2)");
        builder.Entity<Auction>().Property(a => a.BuyItNowPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Bid>().Property(b => b.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<ApplicationUser>().Property(u => u.WalletBalance).HasColumnType("decimal(18,2)");
        builder.Entity<AutoBid>().Property(ab => ab.MaxAmount).HasColumnType("decimal(18,2)");

        // SQL Check Constraint for non-negative balance
        builder.Entity<ApplicationUser>()
            .ToTable(t => t.HasCheckConstraint("CK_ApplicationUser_WalletBalance_Positive", "[WalletBalance] >= 0"));

        // Global Query Filters
        builder.Entity<Auction>().HasQueryFilter(a => !a.IsDeleted);
        builder.Entity<AuctionImage>().HasQueryFilter(i => !i.Auction.IsDeleted);
        builder.Entity<AuctionWatchlist>().HasQueryFilter(w => !w.Auction.IsDeleted);
        builder.Entity<AutoBid>().HasQueryFilter(ab => !ab.Auction.IsDeleted);
        builder.Entity<Bid>().HasQueryFilter(b => !b.Auction.IsDeleted);
        builder.Entity<Review>().HasQueryFilter(r => !r.Auction.IsDeleted);

        // Watchlist unique
        builder.Entity<AuctionWatchlist>()
            .HasIndex(w => new { w.UserId, w.AuctionId })
            .IsUnique();
    }
}