using DotNetCore.CAP;
using MediatR;
using ModuleB.Domain;
using ModuleB.Infrastructure;
using ModuleB.Integration.Query;
using ModuleB.Query;

namespace ModuleB.Application;

public sealed record PlaceOrderCommand(Guid ProductId, int Quantity) : IRequest<OrderDto?>;

// Returns null when quantity is not a positive whole number (FR-006, SC-006) so the
// endpoint can map it to 400 without persisting anything.
public sealed class PlaceOrderCommandHandler(
    ModuleBDbContext dbContext,
    ICapPublisher capPublisher) : IRequestHandler<PlaceOrderCommand, OrderDto?>
{
    public async Task<OrderDto?> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
        {
            return null;
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            PlacedAtUtc = DateTime.UtcNow,
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Fire-and-forget from the caller's perspective (FR-007) - publish happens after
        // the order is durably persisted, so a delivery delay never blocks/rolls back placement.
        var integrationEvent = new OrderPlacedIntegrationEvent(
            EventId: Guid.NewGuid(),
            ProductId: order.ProductId,
            Quantity: order.Quantity,
            OccurredAtUtc: order.PlacedAtUtc);

        await capPublisher.PublishAsync("moduleb.order.placed", integrationEvent, cancellationToken: cancellationToken);

        return new OrderDto(order.Id, order.ProductId, order.Quantity, order.PlacedAtUtc);
    }
}
