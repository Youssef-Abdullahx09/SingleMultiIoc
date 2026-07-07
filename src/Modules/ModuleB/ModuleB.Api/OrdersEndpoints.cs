using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModuleB.Application;

namespace ModuleB.Api;

public static class OrdersEndpoints
{
    // `moduleProvider` is Module B's own child container - same per-request scoping
    // pattern as Module A's CatalogEndpoints (research.md §5).
    public static void MapOrdersEndpoints(this IEndpointRouteBuilder app, IServiceProvider moduleProvider)
    {
        app.MapGet("/api/module-b/orders", async () =>
        {
            using var scope = moduleProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var orders = await mediator.Send(new GetOrdersQuery());
            return Results.Ok(orders);
        });

        app.MapPost("/api/module-b/orders", async (PlaceOrderRequest request) =>
        {
            using var scope = moduleProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var result = await mediator.Send(new PlaceOrderCommand(request.ProductId, request.Quantity));
            return result is null ? Results.BadRequest("Quantity must be a positive whole number.") : Results.Ok(result);
        });
    }

    public sealed record PlaceOrderRequest(Guid ProductId, int Quantity);
}
