using Microsoft.EntityFrameworkCore;
using SeguroAuto.Domain;

namespace SeguroAuto.Data;

public class SeguroAutoDbContext : DbContext
{
    public SeguroAutoDbContext(DbContextOptions<SeguroAutoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; }
    public DbSet<Policy> Policies { get; set; }
    public DbSet<Claim> Claims { get; set; }
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<PricingRule> PricingRules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Document).IsUnique();
            entity.HasMany(e => e.Policies)
                .WithOne(e => e.Customer)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Policy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PolicyNumber).IsUnique();
            entity.HasMany(e => e.Claims)
                .WithOne(e => e.Policy)
                .HasForeignKey(e => e.PolicyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ClaimNumber).IsUnique();
        });

        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.QuoteNumber).IsUnique();
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}

