using AuctionHub.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuctionHub.Data;

public class AuctionHubDbContext : IdentityDbContext<ApplicationUser>
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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

        // Precision for money
        builder.Entity<Auction>().Property(a => a.CurrentPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Auction>().Property(a => a.StartPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Auction>().Property(a => a.MinIncrease).HasColumnType("decimal(18,2)");
        builder.Entity<Auction>().Property(a => a.BuyItNowPrice).HasColumnType("decimal(18,2)");
        builder.Entity<Bid>().Property(b => b.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<Transaction>().Property(t => t.Amount).HasColumnType("decimal(18,2)");
        builder.Entity<ApplicationUser>().Property(u => u.WalletBalance).HasColumnType("decimal(18,2)");

        // Watchlist unique
        builder.Entity<AuctionWatchlist>()
            .HasIndex(w => new { w.UserId, w.AuctionId })
            .IsUnique();
    }
}