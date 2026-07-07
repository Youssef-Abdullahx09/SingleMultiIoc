using MediatR;
using Microsoft.EntityFrameworkCore;
using ModuleA.Infrastructure;
using IntegrationLayer = ModuleB.Integration.Query.HasOrdersForProduct;

namespace ModuleA.Application.Features.CheckAvailability;

public sealed class CheckAvailabilityCommandHandler(
    ISender sender,
    ModuleADbContext dbContext) : IRequestHandler<CheckAvailabilityCommand, AvailabilityResultDto?>
{
    public async Task<AvailabilityResultDto?> Handle(CheckAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var productExists = await dbContext.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            return null;
        }

        // Synchronous cross-module lookup (FR-002, FR-010) - reflects Module B's current data, not the async notification stream.
        var hasOrders = await sender.Send(new IntegrationLayer.Query(request.ProductId), cancellationToken);
        return new AvailabilityResultDto(request.ProductId, hasOrders);
    }
}