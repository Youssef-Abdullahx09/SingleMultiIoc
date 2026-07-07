using MediatR;

namespace ModuleB.Application.GetOrders;

public sealed record GetOrdersQuery : IRequest<List<OrderDto>>;