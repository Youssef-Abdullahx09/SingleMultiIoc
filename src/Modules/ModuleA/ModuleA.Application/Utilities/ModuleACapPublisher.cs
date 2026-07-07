using DotNetCore.CAP;

namespace ModuleA.Application.Utilities;

// Module A's private, isolated CAP instance is used only for publishing (constitution
// Principle IV, amended) - its outbound schema/group (cap_modulea / modulea.catalog) stay
// independent from the Gateway's global CAP instance, which handles Module A's inbound
// subscription instead. Application code depends on this interface, never on ICapPublisher
// directly, so the outbound instance stays swappable without touching call sites.

public sealed class ModuleACapPublisher(ICapPublisher localCapPublisher) : IModuleACapPublisher
{
    public Task PublishAsync<T>(string name, T contentObj, CancellationToken cancellationToken = default) =>
        localCapPublisher.PublishAsync(name, contentObj, cancellationToken: cancellationToken);
}
