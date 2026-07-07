namespace ModuleA.Domain;

// Internal bookkeeping only (spec Assumptions) - Catalog's record that it received
// an OrderPlacedIntegrationEvent. Not exposed via any public endpoint.
public sealed class OrderReceipt
{
    public Guid Id { get; init; }
    public required Guid IntegrationEventId { get; init; }
    public required Guid ProductId { get; init; }
    public required int Quantity { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public DateTime ReceivedAtUtc { get; init; }
}
