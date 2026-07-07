using MediatR;
using ModuleB.Application.GetOrders;

namespace ModuleB.Application.PlaceOrder;

public sealed record PlaceOrderCommand(Guid ProductId, int Quantity) : IRequest<OrderDto?>;

// Returns null when quantity is not a positive whole number (FR-006, SC-006) so the
// endpoint can map it to 400 without persisting anything.