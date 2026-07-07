using MediatR;
using Microsoft.EntityFrameworkCore;
using ModuleB.Infrastructure;
using ModuleB.Query;

namespace ModuleB.Application;

public sealed record GetOrdersQuery : IRequest<List<OrderDto>>;

public sealed class GetOrdersQueryHandler(ModuleBDbContext dbContext) : IRequestHandler<GetOrdersQuery, List<OrderDto>>
{
    public async Task<List<OrderDto>> Handle(GetOrdersQuery request, CancellationToken cancellationToken) =>
        await dbContext.Orders
            .Select(o => new OrderDto(o.Id, o.ProductId, o.Quantity, o.PlacedAtUtc))
            .ToListAsync(cancellationToken);
}
