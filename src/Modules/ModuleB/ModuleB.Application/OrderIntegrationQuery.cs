// using Microsoft.Extensions.DependencyInjection;
// using ModuleB.Infrastructure;
// using ModuleB.Integration.Query;
//
// namespace ModuleB.Application;
//
// // Registered as a singleton directly on the Gateway's global container, alongside
// // Module A (both are the "Single IoC" variant, constitution Principle III, amended) -
// // Module A's CheckAvailabilityCommandHandler resolves this interface straight from
// // the shared container. It must not hold a scoped ModuleBDbContext directly - it
// // creates a fresh scope per call so the DbContext it uses is always scoped correctly.
// public sealed class OrderIntegrationQuery(IServiceScopeFactory scopeFactory) : IOrderIntegrationQuery
// {
//     public bool HasOrdersForProduct(Guid productId)
//     {
//         using var scope = scopeFactory.CreateScope();
//         var dbContext = scope.ServiceProvider.GetRequiredService<ModuleBDbContext>();
//         return dbContext.Orders.Any(o => o.ProductId == productId);
//     }
// }
