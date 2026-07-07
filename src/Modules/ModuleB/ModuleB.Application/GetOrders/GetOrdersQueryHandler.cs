using MediatR;
using Microsoft.EntityFrameworkCore;
using ModuleB.Infrastructure;

namespace ModuleB.Application.GetOrders;

public sealed class GetOrdersQueryHandler(ModuleBDbContext dbContext) : IRequestHandler<GetOrdersQuery, List<OrderDto>>
{
    public async Task<List<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken) =>
        await dbContext.Orders
            .Select(o => new OrderDto(o.Id, o.ProductId, o.Quantity, o.PlacedAtUtc))
            .ToListAsync(cancellationToken);
}