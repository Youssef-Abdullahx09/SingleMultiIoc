using ModuleA.Domain;

namespace ModuleA.Infrastructure;

internal static class SeedData
{
    public static readonly Guid Product1Id = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Product2Id = new("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Product3Id = new("33333333-3333-3333-3333-333333333333");

    public static readonly Product[] Products =
    [
        new Product
        {
            Id = Product1Id,
            Name = "Wireless Mouse",
            Price = 19.99m,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new Product
        {
            Id = Product2Id,
            Name = "Mechanical Keyboard",
            Price = 79.99m,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        },
        new Product
        {
            Id = Product3Id,
            Name = "USB-C Hub",
            Price = 34.50m,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }
    ];
}
