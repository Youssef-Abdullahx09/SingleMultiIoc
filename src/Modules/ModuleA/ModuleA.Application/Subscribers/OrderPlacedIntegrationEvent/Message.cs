namespace ModuleA.Application.Subscribers.OrderPlacedIntegrationEvent;

public sealed record Message(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAtUtc);
