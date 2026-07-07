using MediatR;

namespace ModuleB.Integration.Query.HasOrdersForProduct;

public sealed record Query(Guid ProductId) : IRequest<bool>;