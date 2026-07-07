using Microsoft.EntityFrameworkCore;
using ModuleB.Domain;

namespace ModuleB.Infrastructure;

public sealed class ModuleBDbContext(DbContextOptions<ModuleBDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("moduleb");

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.ProductId).IsRequired();
            entity.Property(o => o.Quantity).IsRequired();
            entity.Property(o => o.PlacedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Order>().HasData(SeedData.Orders);
    }
}
