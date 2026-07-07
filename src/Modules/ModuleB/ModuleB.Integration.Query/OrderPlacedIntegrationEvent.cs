namespace ModuleB.Integration.Query;

public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAtUtc);
