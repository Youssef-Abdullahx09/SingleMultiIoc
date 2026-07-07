using MediatR;
using Microsoft.EntityFrameworkCore;
using ModuleA.Infrastructure;
using ModuleA.Query;
using ModuleB.Integration.Query;

namespace ModuleA.Application;

public sealed record CheckAvailabilityCommand(Guid ProductId) : IRequest<AvailabilityResultDto?>;

// Returns null when the product id is unknown (FR-003) so the endpoint can map it to 404.
public sealed class CheckAvailabilityCommandHandler(
    ModuleADbContext dbContext,
    IOrderIntegrationQuery orderIntegrationQuery) : IRequestHandler<CheckAvailabilityCommand, AvailabilityResultDto?>
{
    public async Task<AvailabilityResultDto?> Handle(CheckAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return null;
        }

        // Synchronous cross-module lookup (FR-002, FR-010) - reflects Module B's current data, not the async notification stream.
        var hasOrders = orderIntegrationQuery.HasOrdersForProduct(request.ProductId);
        return new AvailabilityResultDto(request.ProductId, hasOrders);
    }
}
