using MediatR;

namespace ModuleA.Application.Features.GetProducts;

public sealed record GetProductsQuery : IRequest<List<ProductDto>>;