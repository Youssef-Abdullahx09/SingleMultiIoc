namespace ModuleB.Domain;

public sealed class Order
{
    public Guid Id { get; init; }
    public required Guid ProductId { get; init; }
    public required int Quantity { get; init; }
    public DateTime PlacedAtUtc { get; init; }
}
