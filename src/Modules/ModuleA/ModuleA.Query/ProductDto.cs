namespace ModuleA.Query;

public sealed record ProductDto(Guid Id, string Name, decimal Price, DateTime CreatedAtUtc);
