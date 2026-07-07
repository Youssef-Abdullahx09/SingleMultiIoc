using DotNetCore.CAP;

namespace ModuleB.Infrastructure;

// Module B's private, isolated CAP instance is used only for publishing (constitution
// Principle IV, amended) - its schema/group (cap_moduleb / moduleb.orders) stay independent
// from the Gateway's global CAP instance. Module B has no CAP subscriber, so unlike Module A
// there is no split subscribe-side concern here - this is its only CAP instance. Application
// code depends on this interface, never on ICapPublisher directly, so the outbound instance
// stays swappable without touching call sites.
public interface IModuleBCapPublisher
{
    Task PublishAsync<T>(string name, T contentObj, CancellationToken cancellationToken = default);
}

public sealed class ModuleBCapPublisher(ICapPublisher localCapPublisher) : IModuleBCapPublisher
{
    public Task PublishAsync<T>(string name, T contentObj, CancellationToken cancellationToken = default) =>
        localCapPublisher.PublishAsync(name, contentObj, cancellationToken: cancellationToken);
}
