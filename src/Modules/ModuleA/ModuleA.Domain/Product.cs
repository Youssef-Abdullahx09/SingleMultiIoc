namespace ModuleA.Domain;

public sealed class Product
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
