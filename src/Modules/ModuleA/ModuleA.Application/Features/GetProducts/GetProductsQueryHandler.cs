using MediatR;
using Microsoft.EntityFrameworkCore;
using ModuleA.Infrastructure;

namespace ModuleA.Application.Features.GetProducts;

public sealed class GetProductsQueryHandler(ModuleADbContext dbContext) : IRequestHandler<GetProductsQuery, List<ProductDto>>
{
    public async Task<List<ProductDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken) =>
        await dbContext.Products
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);
}