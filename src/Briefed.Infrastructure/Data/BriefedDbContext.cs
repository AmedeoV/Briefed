using Briefed.Core.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Briefed.Infrastructure.Data;

public class BriefedDbContext : IdentityDbContext<User>, IDataProtectionKeyContext
{
    public BriefedDbContext(DbContextOptions<BriefedDbContext> options) : base(options)
    {
    }

    public DbSet<Feed> Feeds => Set<Feed>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Summary> Summaries => Set<Summary>();
    public DbSet<TrendingSummary> TrendingSummaries => Set<TrendingSummary>();
    public DbSet<UserFeed> UserFeeds => Set<UserFeed>();
    public DbSet<UserArticle> UserArticles => Set<UserArticle>();
    public DbSet<SavedArticle> SavedArticles => Set<SavedArticle>();
    public DbSet<DeletedArticle> DeletedArticles => Set<DeletedArticle>();
    public DbSet<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey> DataProtectionKeys => Set<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Feed configuration
        modelBuilder.Entity<Feed>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.SiteUrl).HasMaxLength(2000);
        });

        // Article configuration
        modelBuilder.Entity<Article>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => e.Url).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(50000);
            entity.Property(e => e.Author).HasMaxLength(500);
            entity.Property(e => e.ImageUrl).HasMaxLength(2000);
            
            entity.HasOne(e => e.Feed)
                .WithMany(f => f.Articles)
                .HasForeignKey(e => e.FeedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Summary configuration
        modelBuilder.Entity<Summary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            
            entity.HasOne(e => e.Article)
                .WithOne(a => a.Summary)
                .HasForeignKey<Summary>(e => e.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TrendingSummary configuration
        modelBuilder.Entity<TrendingSummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UrlHash).IsRequired().HasMaxLength(64);
            entity.HasIndex(e => e.UrlHash);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Model).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.ExpiresAt);
        });

        // UserFeed configuration (many-to-many)
        modelBuilder.Entity<UserFeed>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.FeedId });
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserFeeds)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Feed)
                .WithMany(f => f.UserFeeds)
                .HasForeignKey(e => e.FeedId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserArticle configuration (many-to-many with read status)
        modelBuilder.Entity<UserArticle>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.ArticleId });
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserArticles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Article)
                .WithMany(a => a.UserArticles)
                .HasForeignKey(e => e.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // SavedArticle configuration
        modelBuilder.Entity<SavedArticle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.SavedArticles)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Article)
                .WithMany(a => a.SavedArticles)
                .HasForeignKey(e => e.ArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.UserId, e.ArticleId }).IsUnique();
        });

        // DeletedArticle configuration (tombstone table)
        modelBuilder.Entity<DeletedArticle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(2000);
            entity.HasIndex(e => e.Url); // Index for fast lookups
            entity.HasIndex(e => e.DeletedAt);
        });
    }
}
