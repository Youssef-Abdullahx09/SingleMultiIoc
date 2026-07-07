using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModuleB.Application;
using ModuleB.Application.GetOrders;
using ModuleB.Application.PlaceOrder;

namespace ModuleB.Api;

public static class OrdersEndpoints
{
    // Module B is the "Single IoC" variant - endpoints resolve IMediator from the
    // Gateway's global service provider via normal minimal API parameter injection,
    // no module-local container/scope involved (same pattern as ModuleA.CatalogEndpoints).
    public static void MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/module-b/orders", async (IMediator mediator) =>
        {
            var orders = await mediator.Send(new GetOrdersQuery());
            return Results.Ok(orders);
        });

        app.MapPost("/api/module-b/orders", async (PlaceOrderRequest request, IMediator mediator) =>
        {
            var result = await mediator.Send(new PlaceOrderCommand(request.ProductId, request.Quantity));
            return result is null ? Results.BadRequest("Quantity must be a positive whole number.") : Results.Ok(result);
        });
    }

    public sealed record PlaceOrderRequest(Guid ProductId, int Quantity);
}
