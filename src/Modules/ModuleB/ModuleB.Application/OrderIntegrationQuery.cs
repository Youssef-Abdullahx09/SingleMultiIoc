using Microsoft.Extensions.DependencyInjection;
using ModuleB.Infrastructure;
using ModuleB.Integration.Query;

namespace ModuleB.Application;

// Registered as a singleton in Module B's own container and handed, as a resolved
// instance, into Module A's container (see research.md §1). It must not hold a
// scoped ModuleBDbContext directly - it creates a fresh scope per call so the
// DbContext it uses is always scoped correctly, regardless of which container's
// lifetime the caller (Module A) is running under.
public sealed class OrderIntegrationQuery(IServiceScopeFactory scopeFactory) : IOrderIntegrationQuery
{
    public bool HasOrdersForProduct(Guid productId)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ModuleBDbContext>();
        return dbContext.Orders.Any(o => o.ProductId == productId);
    }
}
