using ModuleB.Domain;

namespace ModuleB.Infrastructure;

internal static class SeedData
{
    // Matches ModuleA.Infrastructure.SeedData product ids. Duplicated intentionally:
    // modules stay decoupled and must not reference each other's domain types.
    private static readonly Guid Product1Id = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Product2Id = new("22222222-2222-2222-2222-222222222222");

    public static readonly Order[] Orders =
    [
        new Order
        {
            Id = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProductId = Product1Id,
            Quantity = 2,
            PlacedAtUtc = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        },
        new Order
        {
            Id = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ProductId = Product2Id,
            Quantity = 1,
            PlacedAtUtc = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)
        }
    ];
}
