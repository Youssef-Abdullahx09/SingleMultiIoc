namespace ModuleB.Application.PlaceOrder;

public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAtUtc);
