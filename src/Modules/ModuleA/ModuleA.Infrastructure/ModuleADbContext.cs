using Microsoft.EntityFrameworkCore;
using ModuleA.Domain;

namespace ModuleA.Infrastructure;

public sealed class ModuleADbContext(DbContextOptions<ModuleADbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<OrderReceipt> OrderReceipts => Set<OrderReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("modulea");

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired();
            entity.Property(p => p.Price).HasColumnType("decimal(18,2)");
            entity.Property(p => p.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<OrderReceipt>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.IntegrationEventId).IsUnique();
            entity.Property(r => r.ProductId).IsRequired();
            entity.Property(r => r.Quantity).IsRequired();
            entity.Property(r => r.OccurredAtUtc).IsRequired();
            entity.Property(r => r.ReceivedAtUtc).IsRequired();
        });
    }
}
