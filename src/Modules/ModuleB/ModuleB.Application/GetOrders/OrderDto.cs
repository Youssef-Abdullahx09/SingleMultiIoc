namespace ModuleB.Application.GetOrders;

public sealed record OrderDto(Guid Id, Guid ProductId, int Quantity, DateTime PlacedAtUtc);
