namespace ModuleB.Query;

public sealed record OrderDto(Guid Id, Guid ProductId, int Quantity, DateTime PlacedAtUtc);
