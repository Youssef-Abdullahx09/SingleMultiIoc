namespace ModuleA.Application.Features.CheckAvailability;

public sealed record AvailabilityResultDto(Guid ProductId, bool HasOrders);
