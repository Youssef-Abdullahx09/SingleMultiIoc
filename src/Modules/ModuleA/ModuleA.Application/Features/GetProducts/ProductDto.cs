namespace ModuleA.Application.Features.GetProducts;

public sealed record ProductDto(Guid Id, string Name, decimal Price, DateTime CreatedAtUtc);
