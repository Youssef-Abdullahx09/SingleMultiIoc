using MediatR;
using Microsoft.EntityFrameworkCore;
using ModuleB.Infrastructure;
using IntegrationLayer = ModuleB.Integration.Query.HasOrdersForProduct;

namespace ModuleB.Application.HasOrdersForProduct;

public sealed class Handler(
    ModuleBDbContext dbContext) : IRequestHandler<IntegrationLayer.Query, bool>
{
    public async Task<bool> Handle(IntegrationLayer.Query request, CancellationToken cancellationToken)
    {
        return await dbContext.Orders.AnyAsync(o => o.ProductId == request.ProductId, cancellationToken);
    }
}