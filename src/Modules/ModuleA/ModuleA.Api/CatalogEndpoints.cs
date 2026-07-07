using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModuleA.Application;

namespace ModuleA.Api;

public static class CatalogEndpoints
{
    // `moduleProvider` is Module A's own child container (research.md §5) - each request
    // opens its own scope from it so IMediator/DbContext resolve from Module A's isolated
    // DI, never from the Gateway's root container.
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app, IServiceProvider moduleProvider)
    {
        app.MapGet("/api/module-a/products", async () =>
        {
            using var scope = moduleProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var products = await mediator.Send(new GetProductsQuery());
            return Results.Ok(products);
        });

        app.MapPost("/api/module-a/products/{id:guid}/check-availability", async (Guid id) =>
        {
            using var scope = moduleProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new CheckAvailabilityCommand(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
