using MediatR;

namespace ModuleA.Application.Features.CheckAvailability;

public sealed record CheckAvailabilityCommand(Guid ProductId) : IRequest<AvailabilityResultDto?>;

// Returns null when the product id is unknown (FR-003) so the endpoint can map it to 404.