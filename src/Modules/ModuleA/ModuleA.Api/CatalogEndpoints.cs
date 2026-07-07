using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModuleA.Application;
using ModuleA.Application.Features.CheckAvailability;
using ModuleA.Application.Features.GetProducts;

namespace ModuleA.Api;

public static class CatalogEndpoints
{
    // Module A is the "Single IoC" variant - endpoints resolve IMediator from the
    // Gateway's global service provider via normal minimal API parameter injection,
    // no module-local container/scope involved.
    public static void MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/module-a/products", async (IMediator mediator) =>
        {
            var products = await mediator.Send(new GetProductsQuery());
            return Results.Ok(products);
        });

        app.MapPost("/api/module-a/products/{id:guid}/check-availability", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new CheckAvailabilityCommand(id));
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
    }
}
