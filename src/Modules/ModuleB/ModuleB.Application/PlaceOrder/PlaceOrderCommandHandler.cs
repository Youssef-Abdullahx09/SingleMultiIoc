using MediatR;
using ModuleB.Application.GetOrders;
using ModuleB.Domain;
using ModuleB.Infrastructure;

namespace ModuleB.Application.PlaceOrder;

public sealed class PlaceOrderCommandHandler(
    ModuleBDbContext dbContext,
    IModuleBCapPublisher capPublisher) : IRequestHandler<PlaceOrderCommand, OrderDto?>
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

        // Order row and outbox row commit atomically (transactional outbox) - actual broker
        // delivery still happens after commit via CAP's own dispatcher, so it never blocks or
        // rolls back the response (FR-007); this only guarantees the outbox row is never lost
        // or duplicated relative to the order row.
        await using var transaction = await capPublisher.BeginTransactionAsync(dbContext.Database, cancellationToken: cancellationToken);

        var integrationEvent = new OrderPlacedIntegrationEvent(
            EventId: Guid.NewGuid(),
            ProductId: order.ProductId,
            Quantity: order.Quantity,
            OccurredAtUtc: order.PlacedAtUtc);

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        await capPublisher.PublishAsync("moduleb.order.placed", integrationEvent, cancellationToken: cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OrderDto(order.Id, order.ProductId, order.Quantity, order.PlacedAtUtc);
    }
}