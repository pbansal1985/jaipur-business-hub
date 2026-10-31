using JaipurHub.Api.Domain;
using Microsoft.EntityFrameworkCore;
namespace JaipurHub.Api.Infrastructure;
public sealed class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
 public DbSet<User> Users=>Set<User>(); public DbSet<Business> Businesses=>Set<Business>(); public DbSet<Listing> Listings=>Set<Listing>();
 public DbSet<Lead> Leads=>Set<Lead>(); public DbSet<Quotation> Quotations=>Set<Quotation>(); public DbSet<Review> Reviews=>Set<Review>();
 public DbSet<SavedBusiness> SavedBusinesses=>Set<SavedBusiness>(); public DbSet<RefreshSession> RefreshSessions=>Set<RefreshSession>(); public DbSet<AuditLog> AuditLogs=>Set<AuditLog>();
 protected override void OnModelCreating(ModelBuilder b)
 {
  b.Entity<User>().HasIndex(x=>x.Email).IsUnique(); b.Entity<User>().HasIndex(x=>x.Phone).IsUnique();
  b.Entity<User>().Property(x=>x.Email).HasMaxLength(320); b.Entity<User>().Property(x=>x.Role).HasMaxLength(32);
  b.Entity<Business>().HasIndex(x=>x.Slug).IsUnique(); b.Entity<Business>().HasIndex(x=>new{x.City,x.Area});
  b.Entity<Business>().HasOne<User>().WithMany().HasForeignKey(x=>x.OwnerId).OnDelete(DeleteBehavior.Restrict);
  b.Entity<Listing>().HasOne(x=>x.Business).WithMany().HasForeignKey(x=>x.BusinessId).OnDelete(DeleteBehavior.Cascade);
  b.Entity<Listing>().HasIndex(x=>new{x.Kind,x.Category,x.IsActive}); b.Entity<Lead>().HasIndex(x=>new{x.Status,x.City,x.CreatedAt});
  b.Entity<Quotation>().HasIndex(x=>new{x.LeadId,x.SupplierBusinessId}).IsUnique();
  b.Entity<Review>().HasCheckConstraint("CK_Reviews_Rating","[Rating] BETWEEN 1 AND 5");
  b.Entity<SavedBusiness>().HasKey(x=>new{x.UserId,x.BusinessId}); b.Entity<RefreshSession>().HasIndex(x=>x.TokenHash).IsUnique();
  b.Entity<AuditLog>().HasIndex(x=>x.CreatedAt);
 }
}